using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;

namespace namHub_FastFood.Controller.ADMIN
{
    [Route("api/customer-manage-for-admin")]
    [ApiController]
    public class CustomersManageController : ControllerBase
    {
        private readonly namHUBDbContext _context;
        public CustomersManageController(namHUBDbContext context)
        {
            _context = context;
        }

        // Xem thông tin của khách hàng
        [HttpGet("get-customer-list")]
        [Authorize( Roles = "ADMIN" )]
        public async Task<IActionResult> GetCL()
        {
            var cusList = await _context.Customers
            .Select(c => new
            {
                c.CustomerId,
                c.FullName,
                c.UserImage,
                c.Phone,
                c.Email,
                c.CreatedAt,
                c.UpdatedAt,
                CustomerAddress = c.Addresses.FirstOrDefault(a => a.IsDefault == true) != null
                ? $"{c.Addresses.FirstOrDefault(a => a.IsDefault == true).AddressLine1}, {(c.Addresses.FirstOrDefault(a => a.IsDefault == true).AddressLine2 != null ? c.Addresses.FirstOrDefault(a => a.IsDefault == true).AddressLine2 + ", " : "")}{c.Addresses.FirstOrDefault(a => a.IsDefault == true).City}"
                : "Không có địa chỉ mặc định!"

            })
            .ToListAsync();


            return Ok(cusList);
        }

        // Xem danh sách đơn hàng của người dùng 
        [HttpGet("get-customer-orders-for-admin/{customerId}")]
        [Authorize( Roles = "ADMIN" )]
        public async Task<IActionResult> GetCusOrders(int customerId)
        {
            if (customerId <= 0)
            {
                return BadRequest("ID người dùng không hợp lệ.");
            }

            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.CustomerId == customerId);
            if (customer == null)
            {
                return NotFound("Không tìm thấy người dùng!");
            }

            var customerOrders = await _context.Orders
                .Where(o => o.CustomerId == customerId)
                .Select(order => new
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
                })
                .ToListAsync();

            if (customerOrders == null || customerOrders.Count == 0)
            {
                return NotFound("Người dùng này chưa có đơn hàng nào.");
            }

            return Ok(customerOrders);
        }

        // Xem chi tiết đơn hàng của người dùng
        [HttpGet("get-customer-order-items-for-admin/{orderId}")]
        [Authorize( Roles = "ADMIN, DELIVER" )]
        public async Task<IActionResult> GetOrderItems(int orderId)
        {
            if (orderId <= 0)
            {
                return BadRequest("ID đơn hàng không hợp lệ.");
            }

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
            {
                return NotFound("Không tìm thấy đơn hàng.");
            }

            var orderItems = await _context.OrderItems
                .Where(oi => oi.OrderId == orderId)
                .Select(oi => new
                {
                    oi.OrderItemId,
                    ProductName = oi.Product.ProductName,
                    Price = oi.UnitPrice,
                    Quantity = oi.Quantity,
                    TotalPrice = oi.TotalPrice,
                })
                .ToListAsync();

            if (orderItems == null || orderItems.Count == 0)
            {
                return NotFound("Không có sản phẩm nào trong đơn hàng này.");
            }

            return Ok(orderItems);
        }


    }
}
