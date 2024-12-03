using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;

namespace namHub_FastFood.Controller.ADMIN
{
    [Route("api/orders-manage-for-admin")]
    [ApiController]
    [Authorize(Roles = "ADMIN")]
    public class OrdersManageController : ControllerBase
    {
        private readonly namHUBDbContext _context;
        public OrdersManageController(namHUBDbContext context)
        {
            _context = context;
        }

        [HttpGet("get-orders-list")]
        public async Task<IActionResult> Get([FromQuery] string? status)
        {
            // Nếu status có giá trị, kiểm tra nếu status không phải là "Pending", "Completed" hoặc "Failed"
            if (!string.IsNullOrEmpty(status) && !new[] { "Pending", "Completed", "Failed" }.Contains(status))
            {
                return BadRequest("Invalid order status.");
            }

            // Query đơn hàng, nếu có status thì lọc theo status, nếu không thì lấy tất cả
            var ordersQuery = _context.Orders
                .Include(o => o.Customer) // Eager loading bảng Customer
                .ThenInclude(c => c.Addresses) // Eager loading bảng Addresses của Customer
                .Include(o => o.Payments) // Eager loading bảng Payments
                .Select(o => new
                {
                    o.OrderId,
                    o.CustomerId,
                    o.Customer.FullName,
                    o.Customer.Phone,
                    // Lấy địa chỉ mặc định
                    CustomerAddress = o.Customer.Addresses
                        .Where(a => a.IsDefault == true)
                        .Select(a => $"{a.AddressLine1}, {a.City}")
                        .FirstOrDefault(),
                    o.OrderDate,
                    o.Status,
                    o.TotalAmount,
                    o.DiscountCodeUsed,
                    o.DiscountAmount,
                    o.TotalAfterDiscount,
                    // Sử dụng biến tạm cho Payments
                    Payment = o.Payments.OrderByDescending(p => p.PaymentDate).FirstOrDefault(),
                    // Điều kiện sử dụng ternary operator cho PaymentStatus
                    PaymentStatus = o.Payments.FirstOrDefault() != null && o.Payments.FirstOrDefault().PaymentStatus == "Completed" ? "Đã Thanh Toán" :
                                    o.Payments.FirstOrDefault() != null && o.Payments.FirstOrDefault().PaymentStatus == "Failed" ? "Thanh Toán Thất Bại" :
                                    "Chưa Thanh Toán"
                });

            // Lọc theo status nếu có giá trị
            if (!string.IsNullOrEmpty(status))
            {
                ordersQuery = ordersQuery.Where(o => o.Status == status);
            }

            // Sắp xếp theo ngày đặt hàng
            var orders = await ordersQuery
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return Ok(orders);
        }




        [Authorize(Roles = "ADMIN,DELIVER,USER")]
        [HttpGet("get-orders-history/{orderId}")]
        public async Task<IActionResult> GetOrderHistory(int orderId)
        {
            // Kiểm tra nếu orderId có tồn tại trong Orders
            var orderExists = await _context.Orders.AnyAsync(o => o.OrderId == orderId);
            if (!orderExists)
            {
                return NotFound(new { Message = "Order not found." });
            }

            // Lấy danh sách lịch sử đơn hàng dựa trên OrderId
            var orderHistory = await _context.OrderStatusHistories
                .Where(h => h.OrderId == orderId)
                .Select(h => new OrderStatusHistoryDto
                {
                    HistoryId = h.StatusHistoryId,
                    OrderId = h.OrderId.Value,
                    CustomerName = h.Order.Customer.FullName,
                    Status = h.Status,
                    UpdatedAt = h.StatusDate.Value,
                    UpdatedBy = h.UpdatedBy ?? "Unknow User"
                })
                .ToListAsync();

            return Ok(orderHistory);
        }
        [Authorize(Roles = "ADMIN,EMPLOYEE,DELIVER")]
        [HttpPost("add-new-state-for-order-history/{orderId}")]
        public async Task<IActionResult> AddNewState(int orderId, [FromBody]string status)
        {
            // Kiểm tra nếu orderId có tồn tại trong Orders
            var orderExists = await _context.Orders.AnyAsync(o => o.OrderId == orderId);
            if (!orderExists)
            {
                return NotFound(new { Message = "Order not found." });
            }

            var userId = int.Parse(User.FindFirst("user_id")?.Value);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            var updatedBy = user?.Username ?? "Unknown User"; // Nếu không tìm thấy, gán giá trị mặc định

            // Lấy danh sách lịch sử đơn hàng dựa trên OrderId
            var orderHistory = new OrderStatusHistory
            {
                OrderId = orderId,
                Status = status,
                StatusDate = DateTime.Now,
                UpdatedBy = updatedBy,
            };

            // Thêm và lưu vào database
            _context.OrderStatusHistories.Add(orderHistory);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Order status updated successfully." });
        }
    }
    public class OrderStatusHistoryDto
    {
        public int HistoryId { get; set; }
        public int OrderId { get; set; }
        public string CustomerName { get; set; }
        public string Status { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string UpdatedBy { get; set; }
    }

}
