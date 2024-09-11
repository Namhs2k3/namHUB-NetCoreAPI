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
        public DeliversController(namHUBDbContext context)
        {
            _context = context;
        }

        // Xem đơn hàng nào đang đợi đc giao
        // Các phương thức xem lịch sử trạng thái đơn hàng và thêm trạng thái mới viết ở bên OrdersManage
        [HttpGet("get-orders-list-for-deliver")]
        public async Task<IActionResult> Get()
        {
            var orders = await _context.Orders
                .Include(o => o.Customer) // Eager loading bảng Customer
                .ThenInclude(c => c.Addresses) // Eager loading bảng Addresses của Customer
                .Include(o => o.Payments) // Eager loading bảng Payments
                .Include(o => o.OrderStatusHistories) // Include OrderStatusHistories để lọc trạng thái đơn hàng
                .Where(o => o.OrderStatusHistories
                    .OrderByDescending(os => os.StatusDate) // Lấy trạng thái mới nhất
                    .FirstOrDefault().Status == "Ready") // Lọc đơn hàng có trạng thái "Ready"
                .Select(o => new
                {
                    o.OrderId,
                    o.CustomerId,
                    o.Customer.FullName,
                    o.Customer.Phone,
                    CustomerAddress = o.Customer.Addresses
                        .Where(a => a.IsDefault == true)
                        .Select(a => $"{a.AddressLine1}, {a.City}")
                        .FirstOrDefault(), // Lấy địa chỉ mặc định
                    o.OrderDate,
                    o.Status,
                    o.TotalAmount,
                    PaymentMethod = o.Payments.FirstOrDefault().PaymentMethod // Lấy phương thức thanh toán
                })
                .OrderBy(o => o.OrderDate) // Sắp xếp theo ngày đặt hàng
                .ToListAsync();

            return Ok(orders);
        }

    }
}
