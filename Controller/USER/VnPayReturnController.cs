using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;

namespace namHub_FastFood.Controller.USER
{
    [ApiController]
    [Route("api/vnpay")]
    public class VnPayReturnController : ControllerBase
    {
        private readonly ILogger<VnPayReturnController> _logger;
        private readonly IConfiguration _configuration;
        private readonly namHUBDbContext _context;

        public VnPayReturnController(ILogger<VnPayReturnController> logger, IConfiguration configuration, namHUBDbContext context)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet("return")]
        public async Task<IActionResult> VnPayReturn()
        {
            _logger.LogInformation("Begin VNPAY Return, URL={Url}", Request.Path + Request.QueryString);

            if (Request.Query.Count > 0)
            {
                string vnp_HashSecret = _configuration["VNPay:vnp_HashSecret"]; // Chuỗi bí mật từ cấu hình
                var vnpayData = Request.Query;

                VnPayLibrary vnpay = new VnPayLibrary();

                foreach (var param in vnpayData)
                {
                    if (!string.IsNullOrEmpty(param.Key) && param.Key.StartsWith("vnp_"))
                    {
                        vnpay.AddResponseData(param.Key, param.Value);
                    }
                }

                // Parse VNPay response data
                string txnRef = vnpay.GetResponseData("vnp_TxnRef");
                if (!long.TryParse(txnRef, out long orderId))
                {
                    _logger.LogError("Invalid Order ID: {TxnRef}", txnRef);
                    return BadRequest("Invalid Order ID.");
                }

                string vnpayTranIdStr = vnpay.GetResponseData("vnp_TransactionNo");
                if (!long.TryParse(vnpayTranIdStr, out long vnpayTranId))
                {
                    _logger.LogError("Invalid Transaction No: {vnp_TransactionNo}", vnpayTranIdStr);
                    return BadRequest("Invalid Transaction No.");
                }

                string vnp_ResponseCode = vnpay.GetResponseData("vnp_ResponseCode");
                string vnp_TransactionStatus = vnpay.GetResponseData("vnp_TransactionStatus");
                string vnp_SecureHash = vnpayData["vnp_SecureHash"];
                string terminalID = vnpayData["vnp_TmnCode"];
                long vnp_Amount = Convert.ToInt64(vnpay.GetResponseData("vnp_Amount")) / 100;
                string bankCode = vnpayData["vnp_BankCode"];

                bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, vnp_HashSecret);

                if (checkSignature)
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var order = await _context.Orders
                            .Include(o => o.Payments)
                            .FirstOrDefaultAsync(o => o.OrderId == orderId);

                        if (order == null)
                        {
                            _logger.LogError("Order not found: {OrderId}", orderId);
                            return BadRequest("Order not found.");
                        }

                        if (vnp_ResponseCode == "00" && vnp_TransactionStatus == "00")
                        {
                            _logger.LogInformation("Thanh toán thành công, OrderId={OrderId}, VNPAY TranId={TranId}", orderId, vnpayTranId);

                            // Tìm Payment đang ở trạng thái Pending
                            var payment = order.Payments.FirstOrDefault(p => p.PaymentStatus == "Pending");
                            if (payment != null)
                            {
                                payment.PaymentStatus = "Completed";
                                payment.TransactionId = vnpayTranIdStr;
                                payment.VnPayResponse = vnp_SecureHash;
                                payment.PaymentDate = DateTime.UtcNow;

                                _context.Payments.Update(payment);

                                // Cập nhật trạng thái đơn hàng
                                order.Status = "Completed";
                                _context.Orders.Update(order);

                                // Cập nhật mã giảm giá nếu có
                                if (!string.IsNullOrEmpty(order.DiscountCodeUsed))
                                {
                                    var discountCode = await _context.DiscountCodes
                                        .FirstOrDefaultAsync(dc => dc.Code == order.DiscountCodeUsed && dc.IsActive);

                                    if (discountCode != null)
                                    {
                                        if (discountCode.IsSingleUse)
                                        {
                                            discountCode.IsActive = false;
                                        }

                                        if (discountCode.MaxUsageCount.HasValue)
                                        {
                                            discountCode.CurrentUsageCount += 1;
                                            if (discountCode.CurrentUsageCount >= discountCode.MaxUsageCount.Value)
                                            {
                                                discountCode.IsActive = false;
                                            }
                                        }

                                        // Tạo bản ghi UsedDiscount
                                        var usedDiscount = new UsedDiscount
                                        {
                                            DiscountId = discountCode.DiscountId,
                                            CustomerId = order.CustomerId.Value,
                                            UsedAt = DateTime.UtcNow
                                        };

                                        _context.UsedDiscounts.Add(usedDiscount);
                                        _context.DiscountCodes.Update(discountCode);
                                    }
                                }

                                // Xóa giỏ hàng và các mục giỏ hàng nếu tồn tại
                                var cart = await _context.Carts
                                    .Include(c => c.CartItems)
                                    .FirstOrDefaultAsync(c => c.UserId == order.CustomerId.Value);

                                if (cart != null)
                                {
                                    _context.CartItems.RemoveRange(cart.CartItems);
                                    _context.Carts.Remove(cart);
                                    _logger.LogInformation("Đã xóa giỏ hàng với CartId={CartId} cho UserId={UserId}", cart.CartId, order.CustomerId.Value);
                                }

                                // Lưu thay đổi vào database
                                await _context.SaveChangesAsync();
                                await transaction.CommitAsync();

                                return Ok(new
                                {
                                    Message = "Thanh toán thành công!",
                                    OrderId = orderId,
                                    PaymentStatus = payment.PaymentStatus
                                });
                            }
                            else
                            {
                                _logger.LogWarning("No pending payment found for OrderId={OrderId}", orderId);
                                return BadRequest("No pending payment found for this order.");
                            }
                        }
                        else
                        {
                            _logger.LogInformation("Thanh toán lỗi, OrderId={OrderId}, VNPAY TranId={TranId}, ResponseCode={ResponseCode}", orderId, vnpayTranId, vnp_ResponseCode);

                            // Xử lý thanh toán lỗi: cập nhật Payment và Order nếu cần
                            var payment = order.Payments.FirstOrDefault(p => p.PaymentStatus == "Pending");
                            if (payment != null)
                            {
                                payment.PaymentStatus = "Failed";
                                payment.VnPayResponse = vnp_SecureHash;
                                payment.PaymentDate = DateTime.UtcNow;

                                _context.Payments.Update(payment);

                                // Lưu lịch sử trạng thái đơn hàng
                                var orderStatusHistory = new OrderStatusHistory
                                {
                                    OrderId = order.OrderId,
                                    Status = "Failed",
                                    StatusDate = DateTime.UtcNow,
                                    UpdatedBy = "VNPay"
                                };
                                _context.OrderStatusHistories.Add(orderStatusHistory);

                                await _context.SaveChangesAsync();
                                await transaction.CommitAsync();
                            }

                            var errorResponse = new
                            {
                                Message = $"Có lỗi xảy ra. Mã lỗi: {vnp_ResponseCode}",
                                TerminalID = terminalID,
                                OrderId = orderId.ToString(),
                                VnpayTranId = vnpayTranId.ToString(),
                                Amount = vnp_Amount.ToString(),
                                BankCode = bankCode
                            };

                            return BadRequest(errorResponse);
                        }
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Error processing VNPay return for OrderId={OrderId}", orderId);
                        return StatusCode(500, "Đã xảy ra lỗi khi xử lý thanh toán.");
                    }
                }
                else
                {
                    _logger.LogInformation("Chữ ký không hợp lệ, InputData={InputData}", Request.Path + Request.QueryString);
                    return BadRequest(new { Message = "Chữ ký không hợp lệ." });
                }
            }
            else
            {
                return BadRequest(new { Message = "Không có dữ liệu đầu vào." });
            }
        }
    }

}
