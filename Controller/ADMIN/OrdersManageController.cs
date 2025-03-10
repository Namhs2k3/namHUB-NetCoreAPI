﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.HUBs;
using namHub_FastFood.Models;

namespace namHub_FastFood.Controller.ADMIN
{
    [Route( "api/orders-manage-for-admin" )]
    [ApiController]
    public class OrdersManageController : ControllerBase
    {
        private readonly namHUBDbContext _context;
        private readonly IHubContext<OrderHub> _hubContext;

        public OrdersManageController( namHUBDbContext context, IHubContext<OrderHub> hubContext )
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet( "get-orders-list" )]
        [Authorize( Roles = "ADMIN, EMPLOYEE" )]
        public async Task<IActionResult> Get( [FromQuery] string? status,
                                    [FromQuery] DateTime? startDate,
                                    [FromQuery] DateTime? endDate,
                                    [FromQuery] string? deliverName,
                                    [FromQuery] string? userName )
        {
            // Danh sách trạng thái hợp lệ (có thể khai báo tĩnh để tái sử dụng)
            var validStatuses = new[] { "Pending", "Completed", "Failed", "Preparing", "On Delivery", "Ready", "Refunded" };

            // Kiểm tra giá trị status
            if ( !string.IsNullOrEmpty( status ) && !validStatuses.Contains( status ) )
            {
                return BadRequest( "Invalid order status." );
            }

            // Query cơ bản với điều kiện eager loading
            var ordersQuery = _context.Orders
                .Include( o => o.Customer )
                .ThenInclude( c => c.Addresses )
                .Include( o => o.Payments )
                .Include( o => o.OrderStatusHistories )
                .AsQueryable();

            // Lọc theo status nếu có
            if ( !string.IsNullOrEmpty( status ) )
            {
                ordersQuery = ordersQuery.Where( o => o.Status == status );
            }
            // Thêm điều kiện lọc PaymentStatus == "Completed"
            ordersQuery = ordersQuery.Where( o => o.Payments.Any( p => p.PaymentStatus == "Completed" && p.PaymentMethod == "VNPay" || p.PaymentMethod == "Cash" ) );

            // Lọc theo khoảng thời gian (nếu có)
            if ( startDate.HasValue )
            {
                ordersQuery = ordersQuery.Where( o => o.OrderDate >= startDate.Value );
            }
            if ( endDate.HasValue )
            {
                ordersQuery = ordersQuery.Where( o => o.OrderDate <= endDate.Value );
            }

            // Lọc theo tên deliver (nếu có)
            if ( !string.IsNullOrEmpty( deliverName ) )
            {
                ordersQuery = ordersQuery.Where( o =>
                    o.OrderStatusHistories.Any( h =>
                        ( h.Status == "Completed" ) && h.UpdatedBy.Replace( " ", "" ).Contains( deliverName.Replace( " ", "" ) ) ) );
            }

            if ( !string.IsNullOrEmpty( userName ) )
            {
                ordersQuery = ordersQuery.Where( o =>
                    o.Customer.FullName.Replace( " ", "" ).Contains( userName.Replace( " ", "" ) ) );
            }

            // Query ánh xạ kết quả
            var orders = await ordersQuery
                .OrderByDescending( o => o.OrderDate ) // Sắp xếp cuối cùng
                .Select( o => new
                {
                    o.OrderId,
                    o.CustomerId,
                    o.Customer.FullName,
                    o.Customer.Phone,
                    // Lấy địa chỉ mặc định nếu có
                    CustomerAddress = o.Customer.Addresses
                        .Where( a => a.IsDefault == true )
                        .Select( a => $"{a.AddressLine1}, {a.City}" )
                        .FirstOrDefault(),
                    o.OrderDate,
                    o.Status,
                    o.TotalAmount,
                    o.DiscountCodeUsed,
                    o.DiscountAmount,
                    o.TotalAfterDiscount,
                    byDeliver = o.OrderStatusHistories.OrderByDescending( o => o.StatusDate ).FirstOrDefault().UpdatedBy,
                    // Lấy Payment cuối cùng (nếu có)
                    Payment = o.Payments.OrderByDescending( p => p.PaymentDate ).FirstOrDefault(),
                    // Trạng thái thanh toán
                    PaymentStatus = o.Payments
                        .OrderByDescending( p => p.PaymentDate ) // Sắp xếp theo ngày thanh toán
                        .Select( p => p.PaymentStatus == "Completed" ? "Đã Thanh Toán" :
                                     p.PaymentStatus == "Failed" ? "Thanh Toán Thất Bại" : "Chưa Thanh Toán" )
                        .FirstOrDefault() // Lấy kết quả đầu tiên
                } )
                .ToListAsync();

            return Ok( orders );
        }

        [Authorize( Roles = "ADMIN,DELIVER,USER,EMPLOYEE" )]
        [HttpGet( "get-orders-history/{orderId}" )]
        public async Task<IActionResult> GetOrderHistory( int orderId )
        {
            // Kiểm tra nếu orderId có tồn tại trong Orders
            var orderExists = await _context.Orders.AnyAsync( o => o.OrderId == orderId );
            if ( !orderExists )
            {
                return NotFound( new { Message = "Order not found." } );
            }

            // Lấy danh sách lịch sử đơn hàng dựa trên OrderId
            var orderHistory = await _context.OrderStatusHistories
                .Where( h => h.OrderId == orderId )
                .Select( h => new OrderStatusHistoryDto
                {
                    HistoryId = h.StatusHistoryId,
                    OrderId = h.OrderId.Value,
                    CustomerName = h.Order.Customer.FullName,
                    Status = h.Status,
                    UpdatedAt = h.StatusDate.Value,
                    UpdatedBy = h.UpdatedBy ?? "Unknow User"
                } )
                .OrderByDescending( h => h.UpdatedAt )
                .ToListAsync();

            return Ok( orderHistory );
        }

        [Authorize( Roles = "ADMIN,EMPLOYEE,DELIVER" )]
        [HttpPost( "add-new-state-for-order-history/{orderId}" )]
        public async Task<IActionResult> AddNewState( int orderId, [FromBody] string status )
        {
            if ( string.IsNullOrWhiteSpace( status ) )
            {
                return BadRequest( new { Message = "Status cannot be empty." } );
            }

            var userIdClaim = User.FindFirst( "user_id" )?.Value;
            if ( string.IsNullOrEmpty( userIdClaim ) || !int.TryParse( userIdClaim, out var userId ) )
            {
                return Unauthorized( new { Message = "Invalid user." } );
            }

            var order = await _context.Orders.FirstOrDefaultAsync( o => o.OrderId == orderId );
            if ( order == null )
            {
                return NotFound( new { Message = "Order not found." } );
            }

            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync( u => u.UserId == userId );
            var updatedBy = user?.Username ?? "Unknown User";

            // Cập nhật trạng thái đơn hàng
            var orderHistory = new OrderStatusHistory
            {
                OrderId = orderId,
                Status = status,
                StatusDate = DateTime.UtcNow.AddHours(7),
                UpdatedBy = updatedBy,
            };

            order.Status = status;

            // Lưu thay đổi vào database
            _context.OrderStatusHistories.Add( orderHistory );

            try
            {
                await _context.SaveChangesAsync();

                // Gửi thông báo qua SignalR
                await _hubContext.Clients.All.SendAsync( "OrderUpdated", orderId, status );

                return Ok( new { Message = "Order status updated successfully." } );
            }
            catch ( Exception ex )
            {
                return StatusCode( 500, new { Message = "An error occurred.", Details = ex.Message } );
            }
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