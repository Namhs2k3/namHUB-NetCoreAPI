using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;

namespace namHub_FastFood.Controller.DELIVER
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "ADMIN,DELIVER")]
    public class DeliversController : ControllerBase
    {
        private readonly namHUBDbContext _context;
        private readonly ILogger<DeliversController> _logger;
        public DeliversController(namHUBDbContext context, ILogger<DeliversController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Xem đơn hàng nào đang đợi đc giao
        // Các phương thức xem lịch sử trạng thái đơn hàng và thêm trạng thái mới viết ở bên OrdersManage
        [HttpGet("get-orders-list-for-deliver")]
        public async Task<IActionResult> Get()
        {
            var orders = await _context.Orders
                        .Include( o => o.Customer ) // Eager loading bảng Customer
                        .ThenInclude( c => c.Addresses ) // Eager loading bảng Addresses của Customer
                        .Include( o => o.Payments ) // Eager loading bảng Payments
                        .Include( o => o.OrderStatusHistories ) // Include OrderStatusHistories để lọc trạng thái đơn hàng
                        .Where( o => new[] { "Ready", "On Delivery" }
                            .Contains( o.OrderStatusHistories
                                .OrderByDescending( os => os.StatusDate ) // Lấy trạng thái mới nhất
                                .FirstOrDefault().Status ) ) // Lọc đơn hàng có trạng thái "Ready" hoặc "On Delivery"
                        .Select( o => new
                        {
                            o.OrderId,
                            o.CustomerId,
                            o.Customer.FullName,
                            o.Customer.Phone,
                            CustomerAddress = o.Customer.Addresses
                                .Where( a => a.IsDefault == true )
                                .Select( a => $"{a.AddressLine1}, {a.City}" )
                                .FirstOrDefault(), // Lấy địa chỉ mặc định
                            o.OrderDate,
                            o.Status,
                            o.TotalAmount,
                            PaymentMethod = o.Payments.FirstOrDefault().PaymentMethod // Lấy phương thức thanh toán
                        } )
                        .OrderBy( o => o.OrderDate ) // Sắp xếp theo ngày đặt hàng
                        .ToListAsync();


            return Ok(orders);
        }

        [HttpPost("delivery-completed/{orderId}")]
        public async Task<IActionResult> DeliveryCompleted(int orderId)
        {

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Các thao tác cập nhật
                var userId = GetUserIdFromClaims();
                if (userId == null)
                {
                    return Unauthorized("Vui lòng đăng nhập tài khoản có quyền DELIVER để tiếp tục!");
                }
                var existingOrder = await _context.Orders.FindAsync(orderId);
                if (existingOrder == null)
                {
                    return BadRequest("Đơn hàng ko tồn tại");
                }
                existingOrder.Status = "Completed";
                var existingUser = await _context.Users.FindAsync(userId);
                var newOrderStatusHistory = new OrderStatusHistory
                {
                    OrderId = orderId,
                    Status = "Completed",
                    StatusDate = DateTime.Now,
                    UpdatedBy = existingUser?.Username,
                };
                _context.OrderStatusHistories.Add(newOrderStatusHistory);
                var existingPayment = await _context.Payments.SingleOrDefaultAsync(p => p.OrderId == orderId && p.PaymentMethod == "Cash" && p.PaymentStatus == "Pending");
                if (existingPayment != null) 
                { 
                    existingPayment.PaymentStatus = "Completed";
                }
                

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating order status for OrderId={OrderId}", orderId);
                return StatusCode(500, "Đã xảy ra lỗi khi cập nhật trạng thái đơn hàng.");
            }

            return Ok("Cập nhật trạng thái thành thành công");
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
}
