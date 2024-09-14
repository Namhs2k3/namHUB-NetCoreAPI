using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Controller.ADMIN;
using namHub_FastFood.Models;
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

        // Dùng để xuất ra danh sách Categories để lọc, tìm kiếm
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
                        OrderPayStatus = order.Payments.FirstOrDefault() != null && order.Payments.FirstOrDefault().PaymentDate != null
                                         ? "Đã Thanh Toán"
                                         : "Chưa Thanh Toán"
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
            var currentDate = DateTime.UtcNow;

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
            var currentDate = DateTime.UtcNow;
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
                UsedAt = DateTime.UtcNow
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


}
