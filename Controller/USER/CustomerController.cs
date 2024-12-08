using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Controller.ADMIN;
using namHub_FastFood.Models;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace namHub_FastFood.Controller.USER
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        public readonly namHUBDbContext _context;
        private readonly string _uploadFolder;
        public CustomerController(namHUBDbContext dbContext)
        {
            _context = dbContext;

            // Đường dẫn thư mục upload hình ảnh (có thể là thư mục trong wwwroot)
            _uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images");

            // Tạo thư mục nếu nó chưa tồn tại
            if (!Directory.Exists(_uploadFolder))
            {
                Directory.CreateDirectory(_uploadFolder);
            }
        }

        // Dùng để xuất ra danh sách Categories để lọc, tìm kiếm hoặc hiển thị danh sách ở trang chủ
        [HttpGet("get-categories-list")]
        public async Task<IActionResult> GetCategories()
        {
            // Tạo URL đầy đủ (base URL + đường dẫn hình ảnh)
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var categories = await _context.Categories
                .Select(c => new //phải có từ khóa "new"
                {
                    CategoryID = c.CategoryId,
                    CategoryName = c.CategoryName,
                    ImgURL = $"{baseUrl}{c.ImgUrl}",
                    Description = c.Description,
                })
                .ToListAsync();

            return Ok(categories);
        }

        [Authorize(Roles = "ADMIN,EMPLOYEE,DELIVER,USER")]
        [HttpGet("get-customer-orders")]
        public async Task<IActionResult> GetCusOrders()
        {
            // Lấy thông tin customer id từ claim của token
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return Unauthorized("Hãy đăng nhập để xem các đơn hàng của bạn!");
            }
            var cusInfo = await _context.Customers
                .Include(ci => ci.Orders)
                .ThenInclude(o => o.OrderStatusHistories) // Tải trước OrderStatusHistories
                .Include(ci => ci.Orders)
                .ThenInclude(o => o.Payments) // Tải trước Payments
                .Where( ci => ci.UserId == userId)
                .Select(co => new
                {
                    OrdersCount = co.Orders.Count, // Đếm số lượng đơn hàng của khách hàng
                    Orders = co.Orders.Select(order => new // Lấy toàn bộ đơn hàng của khách hàng
                    {
                        OrderId = order.OrderId,
                        Status = order.Status,
                        OrderDate = order.OrderDate,
                        TotalAmount = order.TotalAmount,
                        OrderHistoryStatus = order.OrderStatusHistories.OrderByDescending(o => o.StatusDate).FirstOrDefault() != null
                                             ? order.OrderStatusHistories.OrderByDescending(o => o.StatusDate).FirstOrDefault().Status
                                             : null,
                        OrderPayMethod = order.Payments.FirstOrDefault() != null
                                         ? order.Payments.FirstOrDefault().PaymentMethod
                                         : null,
                        OrderPayStatus = order.Payments.FirstOrDefault().PaymentStatus == "Completed" ? "Đã Thanh Toán" :
                                        order.Payments.FirstOrDefault().PaymentStatus == "Failed" ? "Thanh Toán Thất Bại" :
                                        "Chưa Thanh Toán"
                    }).ToList() // Chuyển về danh sách đơn hàng
                })
                .ToListAsync();

            return Ok(cusInfo); // Trả về dữ liệu dạng JSON
        }

        [Authorize(Roles = "ADMIN,EMPLOYEE,DELIVER,USER")]
        [HttpGet("get-customer-orders-items/{orderID}")]
        public async Task<IActionResult> GetCusOI(int orderID)
        {
            // Lấy thông tin customer id từ claim của token
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return Unauthorized("Hãy đăng nhập để xem chi tiết đơn hàng!");
            }

            // Truy vấn thông tin sản phẩm trong đơn hàng của khách hàng
            var cusOItems = await _context.OrderItems
                .Where(u => u.Order.Customer.UserId == userId && u.Order.OrderId == orderID)
                .Select(oi => new
                {
                    oi.OrderItemId,
                    ProductName = oi.Product.ProductName,
                    oi.UnitPrice,
                    oi.Quantity,
                    oi.TotalPrice,
                })
                .ToListAsync();

            // Kiểm tra nếu không có dữ liệu
            if (cusOItems == null || cusOItems.Count == 0)
            {
                return NotFound("Không có sản phẩm!");
            }

            return Ok(cusOItems);
        }

        // Lấy danh sách mã giảm giá
        [HttpGet("get-active-discount-codes-for-customer")]
        public async Task<IActionResult> GetActiveDiscountCodes()
        {
            var currentDate = DateTime.Now;

            var discountCodes = await _context.DiscountCodes
                .Where(dc => dc.IsActive
                    && dc.StartDate <= currentDate
                    && dc.EndDate >= currentDate
                    && ((!dc.IsSingleUse && dc.CurrentUsageCount < dc.MaxUsageCount) || (dc.IsSingleUse && dc.CurrentUsageCount == 0))) // Kiểm tra single use và max usage count
                .Select(dc => new
                {
                    DiscountId = dc.DiscountId,
                    Code = dc.Code,
                    DiscountValue = dc.DiscountValue,
                    DiscountType = dc.DiscountType,
                    MinOrderValue = dc.MinOrderValue,
                    StartDate = dc.StartDate,
                    EndDate = dc.EndDate,
                    IsActive = dc.IsActive,
                    IsSingleUse = dc.IsSingleUse,
                    MaxUsageCount = dc.MaxUsageCount,
                    CurrentUsageCount = dc.CurrentUsageCount
                })
                .ToListAsync();

            return Ok(discountCodes);
        }

        [HttpPost("apply-discount")]
        [Authorize]
        public async Task<IActionResult> ApplyDiscount([FromBody] ApplyDiscountDto dto)
        {
            // Lấy customerId từ claims
            var customerId = GetUserIdFromClaims();
            if (customerId == null)
            {
                return Unauthorized("Không tìm thấy thông tin người dùng.");
            }

            // Tìm mã giảm giá trong cơ sở dữ liệu
            var discountCode = await _context.DiscountCodes
                .FirstOrDefaultAsync(dc => dc.Code == dto.DiscountCode);

            if (discountCode == null)
            {
                return NotFound("Mã giảm giá không tồn tại.");
            }

            // Kiểm tra xem mã giảm giá có còn hiệu lực không
            if (!discountCode.IsActive)
            {
                return BadRequest("Mã giảm giá đã bị vô hiệu hóa.");
            }

            // Kiểm tra thời gian hiệu lực của mã giảm giá
            var currentDate = DateTime.Now;
            if (currentDate < discountCode.StartDate || currentDate > discountCode.EndDate)
            {
                return BadRequest("Mã giảm giá đã hết hạn sử dụng.");
            }

            // Kiểm tra xem mã giảm giá đã hết số lần sử dụng chưa
            if (discountCode.CurrentUsageCount >= discountCode.MaxUsageCount)
            {
                return BadRequest("Mã giảm giá đã hết số lần sử dụng.");
            }

            // Lấy tổng giá trị đơn hàng từ OrderId
            var orderTotalAmount = await _context.OrderItems
                .Where(oi => oi.OrderId == dto.OrderId)
                .SumAsync(oi => oi.Quantity * oi.UnitPrice);

            // Kiểm tra điều kiện sử dụng mã giảm giá
            if (orderTotalAmount < discountCode.MinOrderValue)
            {
                return BadRequest("Tổng giá trị đơn hàng không đủ điều kiện để sử dụng mã giảm giá.");
            }

            // Tính số tiền giảm giá
            decimal discountAmount = 0;

            if (discountCode.DiscountType == "percent") // Nếu là giảm giá theo phần trăm
            {
                discountAmount = orderTotalAmount * (discountCode.DiscountValue / 100);
            }
            else // Nếu là giảm giá theo số tiền cố định
            {
                discountAmount = discountCode.DiscountValue;
            }

            // Kiểm tra mã giảm giá có phải là mã sử dụng một lần không
            if (discountCode.IsSingleUse)
            {
                var existingUsedDiscount = await _context.UsedDiscounts
                    .FirstOrDefaultAsync(ud => ud.DiscountId == discountCode.DiscountId && ud.CustomerId == customerId);

                if (existingUsedDiscount != null)
                {
                    return BadRequest("Mã giảm giá này đã được sử dụng.");
                }
            }

            // Tính số tiền cần thanh toán
            decimal finalAmount = orderTotalAmount - discountAmount;

            // Tăng CurrentUsageCount lên 1
            discountCode.CurrentUsageCount += 1;
            await _context.SaveChangesAsync();

            // Ghi nhận mã giảm giá đã sử dụng
            var usedDiscount = new UsedDiscount
            {
                DiscountId = discountCode.DiscountId,
                CustomerId = customerId.Value,
                UsedAt = DateTime.Now
            };
            _context.UsedDiscounts.Add(usedDiscount);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                DiscountCode = discountCode.Code,
                orderTotalAmount = orderTotalAmount,
                DiscountAmount = discountAmount,
                FinalAmount = finalAmount
            });
        }

        [HttpGet("get-banner-list")]
        public async Task<IActionResult> GetBannerList()
        {
            // Tạo URL đầy đủ (base URL + đường dẫn hình ảnh)
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var banners = await _context.Banners
                .Where(b => b.IsActive == true && b.StartDate <= DateTime.Now && b.EndDate >= DateTime.Now)
                .Select(b => new
                {
                    b.BannerId,
                    b.Title,
                    imgUrl = $"{baseUrl}{b.ImageUrl}",
                    b.Link,
                    b.DisplayOrder,
                    b.CreatedAt,
                    b.UpdatedAt,
                    b.StartDate,
                    b.EndDate,
                }).OrderBy(b => b.DisplayOrder).ToListAsync();

            return Ok(banners);
        }

        [HttpGet("get-popular-foods")]
        public async Task<IActionResult> GetPopularFood()
        {
            var popularFoods = await _context.Products
                .Where(p => p.IsPopular == true && p.IsHidden == false)
                .Select(p=> new
                {
                    p.ProductId,
                    p.ProductName,
                    p.Description,
                    p.ImageUrl,
                    Price = p.DiscountedPrice ?? p.Price,
                    discountPercentage = p.DiscountPercentage ?? 0,
                }).ToListAsync();

            return Ok(popularFoods);
        }

        [HttpGet("get-food-list")]
        public async Task<IActionResult> GetFood([FromQuery] string? searchTerm, [FromQuery] int? categoryId)
        {
            // Lọc sản phẩm theo IsHidden = false, theo danh mục và theo từ khóa tìm kiếm
            var foodsQuery = _context.Products
                .Where(p => p.IsHidden == false);

            // Nếu có tìm kiếm theo từ khóa, thêm điều kiện tìm kiếm
            if (!string.IsNullOrEmpty(searchTerm))
            {
                foodsQuery = foodsQuery.Where(p => p.ProductName.Contains(searchTerm));
            }

            // Nếu có tìm kiếm theo danh mục, thêm điều kiện lọc danh mục
            if (categoryId.HasValue)
            {
                foodsQuery = foodsQuery.Where(p => p.CategoryId == categoryId.Value);
            }

            // Lấy dữ liệu từ truy vấn
            var foods = await foodsQuery  
                .Select(p => new
                {
                    p.ProductId,
                    p.ProductName,
                    p.Description,
                    p.ImageUrl,
                    p.CategoryId,
                    Price = p.DiscountedPrice ?? p.Price,
                    discountPercentage = p.DiscountPercentage ?? 0,
                })
                .ToListAsync();

            return Ok(foods);
        }

        [HttpPost("add-to-cart")]
        [Authorize]
        public async Task<IActionResult> AddToCart(int foodId)
        {
            var userId = GetUserIdFromClaims();

            if(userId == null)
            {
                return BadRequest("Không tìm thấy người dùng!");
            }

            var existingCart = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == userId);

            int cartId =0;
            if(existingCart == null)
            {
                var newCart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                };

                _context.Add(newCart);
                await _context.SaveChangesAsync();
                cartId = newCart.CartId;
            }
            else 
            { 
                existingCart.UpdatedAt = DateTime.Now;
                cartId = existingCart.CartId;
            }
                

            var existingCartItem = await _context.CartItems.FirstOrDefaultAsync(ci => ci.ProductId == foodId && ci.CartId == cartId);

            var existingFood = await _context.Products.FirstOrDefaultAsync(f => f.ProductId == foodId);
            if(existingFood == null)
            {
                return BadRequest("Món không tồn tại!");
            }

            if (existingCartItem == null)
            {
                existingCartItem = new CartItem
                {
                    CartId = cartId,
                    ProductId = foodId,
                    Quantity = 1,
                    Price = existingFood.DiscountedPrice ?? existingFood.Price,
                };

                _context.Add(existingCartItem);
            }
            else
            {
                existingCartItem.UpdatedAt = DateTime.Now;
                // như này là sai: exitingCartItem.Quantity = exitingCartItem.Quantity++;
                //như này mới đúng
                existingCartItem.Quantity++;
            }

            await _context.SaveChangesAsync();
            return Ok(existingCartItem);
        }

        [Authorize]
        [HttpGet("get-cus-cart-items")]
        public async Task<IActionResult> GetCusCartItems()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return Unauthorized("Người dùng chưa đăng nhập!");
            }

            var existingCart = await _context.Carts
                                            .Include(c => c.CartItems) // Eager loading CartItems
                                            .ThenInclude(ci => ci.Product) // Eager loading Product info (if needed)
                                            .FirstOrDefaultAsync(c => c.UserId == userId);

            if (existingCart == null)
            {
                return NotFound("Không tìm thấy thông tin giỏ hàng");
            }

            var cartItems = existingCart.CartItems
                .Select(ci => new
                {
                    ci.CartItemId,
                    ci.CartId,
                    ci.ProductId,
                    ci.Product.ProductName,
                    ci.Product.Description,
                    ci.Product.ImageUrl,
                    ci.Quantity,
                    ci.Price,
                    DiscountedPrice = ci.Product.DiscountedPrice ?? ci.Price,
                    DiscountPercentage = ci.Product.DiscountPercentage ?? 0,
                }).ToList();

            return Ok(cartItems);
        }


        [Authorize]
        [HttpPost("increase-quantity")]
        public async Task<IActionResult> IncreaseQuantity(int foodId)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return Unauthorized("Người dùng chưa đăng nhập!");
            }

            var existingCart = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == userId);
            if (existingCart == null)
            {
                return NotFound("Không tìm thấy thông tin giỏ hàng");
            }

            var existingCartItem = await _context.CartItems.FirstOrDefaultAsync(p => p.ProductId == foodId && p.CartId == existingCart.CartId);
            if (existingCartItem == null)
            {
                return NotFound("Không tìm thấy sản phẩm trong giỏ hàng");
            }

            existingCartItem.Quantity++;
            await _context.SaveChangesAsync();

            return Ok(existingCartItem);
        }

        [Authorize]
        [HttpPost("decrease-quantity")]
        public async Task<IActionResult> DecreaseQuantity(int foodId) // sửa lại DecreaseQuantity cho đúng
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return Unauthorized("Người dùng chưa đăng nhập!");
            }

            var existingCart = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == userId);
            if (existingCart == null)
            {
                return NotFound("Không tìm thấy thông tin giỏ hàng");
            }

            var existingCartItem = await _context.CartItems.FirstOrDefaultAsync(p => p.ProductId == foodId && p.CartId == existingCart.CartId);

            if (existingCartItem == null)
            {
                return NotFound("Không tìm thấy sản phẩm trong giỏ hàng");
            }

            existingCartItem.Quantity--; // Giảm số lượng

            if (existingCartItem.Quantity <= 0)
            {
                _context.CartItems.Remove(existingCartItem); // Xóa sản phẩm nếu số lượng là 0
            }

            await _context.SaveChangesAsync(); // Cập nhật thay đổi vào DB
            return Ok(existingCartItem);
        }

        [Authorize]
        [HttpDelete("delete-cart-item")]
        public async Task<IActionResult> DeleteCartItem(int foodId)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return Unauthorized("Người dùng chưa đăng nhập!");
            }

            var existingCart = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == userId);
            if (existingCart == null)
            {
                return NotFound("Không tìm thấy thông tin giỏ hàng");
            }

            var existingCartItem = await _context.CartItems.FirstOrDefaultAsync(p => p.ProductId == foodId && p.CartId == existingCart.CartId);

            if (existingCartItem == null)
            {
                return NotFound("Không tìm thấy sản phẩm trong giỏ hàng");
            }

            _context.CartItems.Remove(existingCartItem);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Sản phẩm đã được xóa khỏi giỏ hàng", productId = foodId });
        }

        [Authorize]
        [HttpGet("get-cart-item-count")]
        public async Task<IActionResult> GetCartItemCount()
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return Unauthorized("Người dùng chưa đăng nhập!");
            }

            var existingCart = await _context.Carts.FirstOrDefaultAsync(c => c.UserId == userId);
            if (existingCart == null)
            {
                return Ok(0);
            }

            var existingCartItemCount = await _context.CartItems.Where(ci => ci.CartId == existingCart.CartId).SumAsync(ci => ci.Quantity);

            return Ok(existingCartItemCount);
        }

        #region Các Hàm Thanh Toán Bị Thừa Không Dùng Đến
        [Authorize]
        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
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
                        .FirstOrDefaultAsync(c => c.Code == request.CouponCode && c.IsActive &&
                                                  c.StartDate <= DateTime.Now && c.EndDate >= DateTime.Now &&
                                                  ((!c.IsSingleUse && c.CurrentUsageCount < c.MaxUsageCount) || (c.IsSingleUse && c.CurrentUsageCount == 0)));

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
                    string vnpayUrl = GenerateVnPayUrl(newOrder.OrderId, totalAfterDiscount);

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
                    // Tạo bản ghi Payment với trạng thái Completed
                    var payment = new Payment
                    {
                        OrderId = newOrder.OrderId,
                        PaymentDate = DateTime.Now,
                        PaymentMethod = "Cash",
                        Amount = totalAfterDiscount,
                        PaymentStatus = "Completed"
                    };

                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync();

                    // Cập nhật trạng thái đơn hàng thành Completed
                    newOrder.Status = "Completed";
                    _context.Orders.Update(newOrder);
                    await _context.SaveChangesAsync();

                    // Lưu lịch sử trạng thái đơn hàng
                    var orderStatusHistory = new OrderStatusHistory
                    {
                        OrderId = newOrder.OrderId,
                        Status = "Completed",
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
                            if (couponToUpdate.IsSingleUse)
                            {
                                couponToUpdate.IsActive = false;
                            }

                            if (couponToUpdate.MaxUsageCount.HasValue)
                            {
                                couponToUpdate.CurrentUsageCount += 1;
                                if (couponToUpdate.CurrentUsageCount >= couponToUpdate.MaxUsageCount)
                                {
                                    couponToUpdate.IsActive = false;
                                }
                            }

                            // Tạo bản ghi UsedDiscountCode
                            var usedDiscount = new UsedDiscount
                            {
                                DiscountId = couponToUpdate.DiscountId,
                                CustomerId = customer.CustomerId, // Sử dụng CustomerId đúng
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

                    return Ok(new CheckoutResponse
                    {
                        Message = "Đơn hàng và thanh toán bằng tiền mặt đã được xử lý thành công!",
                        OrderId = newOrder.OrderId,
                        TotalAmount = totalAmount,
                        DiscountAmount = discountAmount,
                        TotalAfterDiscount = totalAfterDiscount,
                        PaymentMethod = "Cash",
                        PaymentStatus = "Completed"
                    });
                }
                else
                {
                    return BadRequest("Phương thức thanh toán không hợp lệ.");
                }
            }
            catch (DbUpdateException dbEx)
            {
                // Ghi log inner exception nếu có
                string errorMessage = dbEx.InnerException != null ? dbEx.InnerException.Message : dbEx.Message;
                await transaction.RollbackAsync();
                return StatusCode(500, $"Đã xảy ra lỗi khi lưu dữ liệu: {errorMessage}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"Đã xảy ra lỗi: {ex.Message}");
            }
        }


        // Endpoint xử lý phản hồi từ VNPay
        [AllowAnonymous]
        [HttpGet("vnpay-return")]
        public async Task<IActionResult> VnPayReturn([FromQuery] VnPayResponse response)
        {
            // Kiểm tra chữ ký để đảm bảo phản hồi từ VNPay là hợp lệ
            // Bạn cần tái tạo chuỗi tham số và so sánh với vnp_SecureHash
            // Dưới đây là ví dụ đơn giản, bạn cần triển khai kiểm tra chữ ký đúng theo hướng dẫn của VNPay

            // Lấy OrderId từ vnp_TxnRef
            var orderIdStr = response.vnp_TxnRef.Replace("ORD", ""); // Giả sử vnp_TxnRef là "ORD{orderId}"
            if (!int.TryParse(orderIdStr, out int orderId))
            {
                return BadRequest("Invalid Order ID");
            }

            var order = await _context.Orders
                .Include(o => o.Payments)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                return BadRequest("Order not found");
            }

            if (response.vnp_ResponseCode == "00")
            {
                // Thanh toán thành công
                var payment = order.Payments.FirstOrDefault(p => p.PaymentStatus == "Pending");
                if (payment != null)
                {
                    payment.PaymentStatus = "Completed";
                    payment.TransactionId = response.vnp_TransactionNo;
                    payment.VnPayResponse = response.vnp_SecureHash;

                    _context.Payments.Update(payment);

                    // Cập nhật trạng thái đơn hàng
                    order.Status = "Completed";
                    _context.Orders.Update(order);

                    // Lưu lịch sử trạng thái đơn hàng
                    var orderStatusHistory = new OrderStatusHistory
                    {
                        OrderId = order.OrderId,
                        Status = "Completed",
                        StatusDate = DateTime.Now,
                        UpdatedBy = "VNPay"
                    };
                    _context.OrderStatusHistories.Add(orderStatusHistory);

                    // Cập nhật mã giảm giá (nếu có)
                    if (!string.IsNullOrEmpty(order.DiscountCodeUsed))
                    {
                        var couponToUpdate = await _context.DiscountCodes
                            .FirstOrDefaultAsync(c => c.Code == order.DiscountCodeUsed);

                        if (couponToUpdate != null)
                        {
                            if (couponToUpdate.IsSingleUse)
                            {
                                couponToUpdate.IsActive = false;
                            }

                            if (couponToUpdate.MaxUsageCount.HasValue)
                            {
                                couponToUpdate.CurrentUsageCount += 1;
                                if (couponToUpdate.CurrentUsageCount >= couponToUpdate.MaxUsageCount)
                                {
                                    couponToUpdate.IsActive = false;
                                }
                            }

                            // Tạo bản ghi UsedDiscountCode
                            var usedDiscount = new UsedDiscount
                            {
                                DiscountId = couponToUpdate.DiscountId,
                                CustomerId = order.CustomerId.Value,
                                UsedAt = DateTime.Now
                            };

                            _context.UsedDiscounts.Add(usedDiscount);
                            _context.DiscountCodes.Update(couponToUpdate);
                        }
                    }

                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        Message = "Thanh toán thành công!",
                        OrderId = order.OrderId,
                        PaymentStatus = "Completed"
                    });
                }
            }
            else
            {
                // Thanh toán không thành công
                var payment = order.Payments.FirstOrDefault(p => p.PaymentStatus == "Pending");
                if (payment != null)
                {
                    payment.PaymentStatus = "Failed";
                    payment.VnPayResponse = response.vnp_SecureHash;
                    _context.Payments.Update(payment);

                    // Lưu lịch sử trạng thái đơn hàng
                    var orderStatusHistory = new OrderStatusHistory
                    {
                        OrderId = order.OrderId,
                        Status = "Failed",
                        StatusDate = DateTime.Now,
                        UpdatedBy = "VNPay"
                    };
                    _context.OrderStatusHistories.Add(orderStatusHistory);

                    await _context.SaveChangesAsync();
                }

                return BadRequest("Thanh toán không thành công.");
            }

            return BadRequest("Invalid response from VNPay.");
        }

        private string GenerateVnPayUrl(int orderId, decimal amount)
        {
            // Thông tin cấu hình VNPay
            string vnpayBaseUrl = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
            string vnp_TmnCode = "VT1L21K5"; // Thay bằng TMN Code của bạn từ VNPay
            string vnp_HashSecret = "RI32OSQM01IF9SUH7MH9M32YZ1W25PRH"; // Thay bằng Hash Secret của bạn từ VNPay
            string vnp_ReturnUrl = "https://localhost:7078/api/customer/vnpay-return"; // URL callback

            // Tạo các tham số cần thiết
            var vnp_Params = new Dictionary<string, string>
    {
                { "vnp_Version", "2.1.0" },
                { "vnp_Command", "pay" },
                { "vnp_TmnCode", vnp_TmnCode },
                { "vnp_Locale", "vn" },
                { "vnp_CurrCode", "VND" },
                { "vnp_TxnRef", $"ORD{orderId}" }, // Mã đơn hàng duy nhất
                { "vnp_OrderInfo", $"Thanh toán đơn hàng #{orderId}" },
                { "vnp_Amount", ((long)(amount * 100)).ToString() }, // VNPay yêu cầu số tiền theo đơn vị VND (không có dấu phẩy)
                { "vnp_ReturnUrl", vnp_ReturnUrl },
                { "vnp_IpAddr", "127.0.0.1" }, // Địa chỉ IP của người dùng
                { "vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss") }
            };

            // Sắp xếp các tham số theo thứ tự ABC
            var sortedParams = vnp_Params.OrderBy(o => o.Key)
                                         .ToDictionary(k => k.Key, v => v.Value);

            // Tạo chuỗi tham số
            string query = string.Join("&", sortedParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));

            // Tạo chữ ký (hash) cho các tham số
            string signData = query + vnp_HashSecret;
            string vnp_SecureHash = ComputeSha256Hash(signData);

            // Thêm chữ ký vào tham số
            string vnp_Url = $"{vnpayBaseUrl}?{query}&vnp_SecureHash={vnp_SecureHash}&vnp_SecureHashType=SHA256";

            return vnp_Url;
        }


        // Hàm tính hash SHA256
        private string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                foreach (var b in bytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        #endregion
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

    public class ApplyDiscountDto
    {
        public string DiscountCode { get; set; }
        public int OrderId { get; set; }
    }

    public class CheckoutRequest
    {
        public string? CouponCode { get; set; }
        public string PaymentMethod { get; set; } = "Cash"; // Mặc định là "Cash"
    }

    public class CheckoutResponse
    {
        public string Message { get; set; }
        public int OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAfterDiscount { get; set; }
        public string PaymentMethod { get; set; }
        public string? VnPayUrl { get; set; } // URL thanh toán VNPay nếu chọn VNPay
        public string PaymentStatus { get; set; }
    }

    public class VnPayResponse
    {
        public string vnp_TxnRef { get; set; } = string.Empty;
        public string vnp_ResponseCode { get; set; } = string.Empty;
        public string vnp_TransactionNo { get; set; } = string.Empty;
        public string vnp_SecureHash { get; set; } = string.Empty;
        // Thêm các thuộc tính khác nếu cần thiết
    }


}
