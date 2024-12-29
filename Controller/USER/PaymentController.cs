using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;

namespace namHub_FastFood.Controller.USER
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly VnPayService _vnPayService;
        private readonly namHUBDbContext _context;
        private readonly IConfiguration _configure;

        public PaymentController(VnPayService vnPayService, namHUBDbContext context, IConfiguration configure)
        {
            _vnPayService = vnPayService;
            _context = context;
            _configure = configure;
        }

        [HttpPost("create-payment")]
        public IActionResult CreatePayment([FromBody] PaymentRequestModel model)
        {
            try
            {
                string paymentUrl = _vnPayService.CreatePaymentUrl(model.OrderId, model.Amount, model.OrderInfo, _configure["VNPay:vnp_Returnurl"], model.IpAddress, model.Locale, model.BankCode);
                return Ok(new { paymentUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [Authorize]
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] PaymentRequestModel request)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return Unauthorized("Người dùng chưa đăng nhập!");
            }

            // Truy xuất Customer dựa trên userId
            var customer = await _context.Customers
                .FirstOrDefaultAsync(c => c.UserId == userId.Value);

            if (customer == null)
            {
                return BadRequest("Không tìm thấy thông tin khách hàng. Vui lòng liên hệ hỗ trợ.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Lấy giỏ hàng của người dùng
                var existingCart = await _context.Carts
                    .Include(c => c.CartItems)
                        .ThenInclude(ci => ci.Product)
                    .FirstOrDefaultAsync(c => c.UserId == userId.Value);

                if (existingCart == null || !existingCart.CartItems.Any())
                {
                    return BadRequest("Giỏ hàng của bạn đang trống!");
                }

                // Tính tổng số tiền trong giỏ hàng
                decimal totalAmount = existingCart.CartItems.Sum(ci => (ci.Product.DiscountedPrice ?? ci.Price) * ci.Quantity);

                // Kiểm tra và áp dụng mã giảm giá (nếu có)
                decimal discountAmount = 0;
                decimal totalAfterDiscount = totalAmount;

                if (!string.IsNullOrEmpty(request.CouponCode))
                {
                    var coupon = await _context.DiscountCodes
                                    .FirstOrDefaultAsync(c =>
                                        c.Code == request.CouponCode &&
                                        c.IsActive &&
                                        c.StartDate <= DateTime.Now &&
                                        c.EndDate >= DateTime.Now &&
                                        (
                                            !c.IsSingleUse || // Không phải loại dùng một lần, hoặc...
                                            !_context.UsedDiscounts.Any(ud => ud.DiscountId == c.DiscountId && ud.CustomerId == customer.CustomerId) // Loại dùng một lần và user chưa sử dụng
                                        ) &&
                                        (
                                            c.MaxUsageCount == null || c.CurrentUsageCount < c.MaxUsageCount // Chỉ kiểm tra số lần sử dụng nếu có MaxUsageCount
                                        )
                                    );

                    if (coupon == null)
                    {
                        return BadRequest("Mã giảm giá không hợp lệ hoặc đã hết hạn.");
                    }

                    if (totalAmount < (coupon.MinOrderValue ?? 0))
                    {
                        return BadRequest($"Tổng đơn hàng phải từ {coupon.MinOrderValue} trở lên để áp dụng mã giảm giá này.");
                    }

                    // Tính toán giảm giá dựa trên loại mã giảm giá
                    if (coupon.DiscountType.Equals("percent", StringComparison.OrdinalIgnoreCase))
                    {
                        discountAmount = totalAmount * (coupon.DiscountValue / 100);
                    }
                    else if (coupon.DiscountType.Equals("amount", StringComparison.OrdinalIgnoreCase))
                    {
                        discountAmount = coupon.DiscountValue;
                    }

                    // Đảm bảo giảm giá không vượt quá tổng đơn hàng
                    discountAmount = Math.Min(discountAmount, totalAmount);
                    totalAfterDiscount = totalAmount - discountAmount;
                }

                var newOrder = new Order
                {
                    CustomerId = customer.CustomerId,
                    OrderDate = DateTime.Now,
                    Status = "Pending",
                    TotalAmount = totalAmount,
                    DiscountCodeUsed = request.CouponCode,
                    DiscountAmount = discountAmount,
                    TotalAfterDiscount = totalAfterDiscount,
                };

                // Thêm đơn hàng mới vào cơ sở dữ liệu trước
                _context.Orders.Add(newOrder);
                await _context.SaveChangesAsync();

                // Sau khi đơn hàng đã được lưu, lấy OrderId và gán cho OrderItems
                foreach (var cartItem in existingCart.CartItems)
                {
                    var orderItem = new OrderItem
                    {
                        OrderId = newOrder.OrderId, // Gán OrderId sau khi Order đã được lưu
                        ProductId = cartItem.ProductId,
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.Product.DiscountedPrice ?? cartItem.Price
                    };
                    _context.OrderItems.Add(orderItem);
                }

                // Lưu các OrderItems vào cơ sở dữ liệu
                await _context.SaveChangesAsync();

                // Xử lý thanh toán dựa trên phương thức
                if (request.PaymentMethod.Equals("VNPay", StringComparison.OrdinalIgnoreCase))
                {
                    // Tạo bản ghi Payment với trạng thái Pending
                    var payment = new Payment
                    {
                        OrderId = newOrder.OrderId,
                        PaymentDate = DateTime.Now,
                        PaymentMethod = "VNPay",
                        Amount = totalAfterDiscount,
                        PaymentStatus = "Pending"
                    };

                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();

                    // Tạo URL thanh toán VNPay
                    string vnpayUrl = _vnPayService.CreatePaymentUrl(newOrder.OrderId, totalAfterDiscount, "order", _configure["VNPay:vnp_Returnurl"], request.IpAddress);

                    // Lưu lịch sử trạng thái đơn hàng
                    var orderStatusHistory = new OrderStatusHistory
                    {
                        OrderId = newOrder.OrderId,
                        Status = "Pending",
                        StatusDate = DateTime.Now,
                        UpdatedBy = "System via VNPayment"
                    };
                    _context.OrderStatusHistories.Add(orderStatusHistory);
                    await _context.SaveChangesAsync();

                    // Hoàn thành giao dịch trước khi trả về
                    await transaction.CommitAsync();

                    // Trả về URL VNPay để người dùng thực hiện thanh toán
                    return Ok(new CheckoutResponse
                    {
                        Message = "Đơn hàng đã được tạo. Vui lòng thanh toán qua VNPay.",
                        OrderId = newOrder.OrderId,
                        TotalAmount = totalAmount,
                        DiscountAmount = discountAmount,
                        TotalAfterDiscount = totalAfterDiscount,
                        PaymentMethod = "VNPay",
                        VnPayUrl = vnpayUrl,
                        PaymentStatus = "Pending"
                    });
                }
                else if (request.PaymentMethod.Equals("Cash", StringComparison.OrdinalIgnoreCase))
                {
                    // Thanh toán bằng tiền mặt
                    var payment = new Payment
                    {
                        OrderId = newOrder.OrderId,
                        PaymentDate = DateTime.Now,
                        PaymentMethod = "Cash",
                        Amount = totalAfterDiscount,
                        PaymentStatus = "Pending"
                    };

                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();

                    // Lưu lịch sử trạng thái đơn hàng
                    var orderStatusHistory = new OrderStatusHistory
                    {
                        OrderId = newOrder.OrderId,
                        Status = "Pending",
                        StatusDate = DateTime.Now,
                        UpdatedBy = "System via Cash Payment"
                    };
                    _context.OrderStatusHistories.Add(orderStatusHistory);
                    await _context.SaveChangesAsync();

                    // Cập nhật mã giảm giá (nếu có)
                    if (!string.IsNullOrEmpty(request.CouponCode))
                    {
                        var couponToUpdate = await _context.DiscountCodes
                            .FirstOrDefaultAsync(c => c.Code == request.CouponCode);

                        if (couponToUpdate != null)
                        {
                            if (couponToUpdate.MaxUsageCount.HasValue)
                            {
                                couponToUpdate.CurrentUsageCount += 1;
                                if (couponToUpdate.CurrentUsageCount >= couponToUpdate.MaxUsageCount)
                                {
                                    couponToUpdate.IsActive = false;
                                }
                            }

                            var usedDiscount = new UsedDiscount
                            {
                                DiscountId = couponToUpdate.DiscountId,
                                CustomerId = customer.CustomerId,
                                UsedAt = DateTime.Now
                            };

                            _context.UsedDiscounts.Add(usedDiscount);
                            _context.DiscountCodes.Update(couponToUpdate);
                            await _context.SaveChangesAsync();
                        }
                    }

                    // Xóa giỏ hàng sau khi tạo đơn hàng thành công
                    _context.CartItems.RemoveRange(existingCart.CartItems);
                    _context.Carts.Remove(existingCart);
                    await _context.SaveChangesAsync();

                    // Hoàn thành giao dịch
                    await transaction.CommitAsync();

                    //return Ok(new CheckoutResponse
                    //{
                    //    Message = "Đơn hàng đã được tạo, vui lòng thanh toán khi nhận hàng!",
                    //    OrderId = newOrder.OrderId,
                    //    TotalAmount = totalAmount,
                    //    DiscountAmount = discountAmount,
                    //    TotalAfterDiscount = totalAfterDiscount,
                    //    PaymentMethod = "Cash",
                    //    PaymentStatus = "Pending"
                    //});
                    return Ok( new { RedirectUrl = $"http://localhost:5173/order-success?orderId={newOrder.OrderId}" } );

                }
                else
                {
                    return BadRequest("Phương thức thanh toán không hợp lệ.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                string errorMessage = dbEx.InnerException != null ? dbEx.InnerException.Message : dbEx.Message;
                await transaction.RollbackAsync();
                //return StatusCode(500, $"Đã xảy ra lỗi khi lưu dữ liệu: {errorMessage}");
                return Ok( new { RedirectUrl = "http://localhost:5173/order-failure" } );

            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                //return StatusCode(500, $"Đã xảy ra lỗi: {ex.Message}");
                return Ok( new { RedirectUrl = "http://localhost:5173/order-failure" } );

            }
        }

        private int? GetUserIdFromClaims()
        {
            var userIdClaim = User.FindFirst("user_id")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
            {
                return null;
            }

            if (int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }

            return null;
        }
    }

    public class PaymentRequestModel
    {
        public string? CouponCode { get; set; }
        public long OrderId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = "Cash"; // Mặc định là "Cash"
        public string OrderInfo { get; set; }
        public string IpAddress { get; set; }
        public string Locale { get; set; } = "vn";
        public string BankCode { get; set; }
    }
}