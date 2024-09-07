using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;

namespace namHub_FastFood.Controller.ADMIN
{
    [Authorize(Roles = "ADMIN")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminStatisticsController : ControllerBase
    {
        private readonly namHUBDbContext _context;

        public AdminStatisticsController(namHUBDbContext context)
        {
            _context = context;
        }

        // 1. Thống kê tổng doanh thu
        [HttpGet("revenue")]
        public async Task<IActionResult> GetRevenue()
        {
            var totalRevenue = await _context.Orders
                .Where(o => o.Status == "Completed") // Chỉ tính các đơn hàng hoàn thành
                .SumAsync(o => o.TotalAmount);       // Tổng doanh thu từ các đơn hàng

            return Ok(totalRevenue);
        }

        // Thống kê doanh thu theo tháng, quý, năm sử dụng OrderDate và Status
        [HttpGet("revenue-by-time")]
        public async Task<IActionResult> GetRevenueByTime([FromQuery] int? year, [FromQuery] int? month, [FromQuery] int? quarter)
        {
            var query = _context.Orders
                .Where(o => o.Status == "Completed") // Chỉ lấy các đơn hàng đã hoàn thành
                .AsQueryable();

            // Lọc theo năm nếu có
            if (year.HasValue)
            {
                query = query.Where(o => o.OrderDate.Value.Year == year.Value);
            }

            // Lọc theo tháng nếu có
            if (month.HasValue)
            {
                query = query.Where(o => o.OrderDate.Value.Month == month.Value);
            }

            // Lọc theo quý nếu có
            if (quarter.HasValue)
            {
                query = query.Where(o => (o.OrderDate.Value.Month - 1) / 3 + 1 == quarter.Value);
            }

            var totalRevenue = await query
                .SumAsync(o => o.TotalAmount);

            return Ok(totalRevenue);
        }

        // Thống kê doanh thu theo khoảng thời gian sử dụng Status và OrderDate
        [HttpGet("revenue-by-date")]
        public async Task<IActionResult> GetRevenueByDate(DateTime startDate, DateTime endDate)
        {
            // Đảm bảo endDate không bao gồm ngày cuối cùng, nên thêm một ngày vào endDate để bao gồm toàn bộ ngày kết thúc
            endDate = endDate.Date.AddDays(1).AddTicks(-1);
            var totalRevenue = await _context.Orders
                .Where(o => o.Status == "Completed" && o.OrderDate >= startDate && o.OrderDate <= endDate)
                .SumAsync(o => o.TotalAmount);

            return Ok(totalRevenue);
        }

        // Thống kê doanh thu theo phương thức thanh toán
        [HttpGet("revenue-by-payment-method")]
        public async Task<IActionResult> GetRevenueByPaymentMethod(string paymentMethod)
        {
            var totalRevenue = await _context.Orders
                .Where(o => o.Status == "Completed" && o.Payments.Any(p => p.PaymentMethod == paymentMethod))
                .SumAsync(o => o.TotalAmount);

            return Ok(totalRevenue);
        }

        // Thống kê doanh thu theo phương thức thanh toán trong khoảng thời gian
        [HttpGet("revenue-by-payment-method-in-period-time")]
        public async Task<IActionResult> GetRevenueByPaymentMethod([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            // Lọc các đơn hàng hoàn thành trong khoảng thời gian
            var revenueByPaymentMethod = await _context.Payments
                .Where(p => p.Order.Status == "Completed" && p.Order.OrderDate >= startDate && p.Order.OrderDate <= endDate)
                .GroupBy(p => p.PaymentMethod) // Nhóm theo phương thức thanh toán
                .Select(group => new
                {
                    PaymentMethod = group.Key,
                    TotalRevenue = group.Sum(p => p.Amount) // Tổng doanh thu cho mỗi phương thức thanh toán
                })
                .OrderByDescending(result => result.TotalRevenue) // Sắp xếp theo doanh thu giảm dần
                .ToListAsync();

            return Ok(revenueByPaymentMethod);
        }

        // Thống kê doanh thu theo danh mục sản phẩm
        [HttpGet("revenue-by-category")]
        public async Task<IActionResult> GetRevenueByCategory()
        {
            var revenueByCategory = await _context.OrderItems
                .Where(oi => oi.Order.Status == "Completed") // Chỉ các đơn hàng đã hoàn thành
                .GroupBy(oi => oi.Product.CategoryId) // Nhóm theo danh mục sản phẩm
                .Select(group => new
                {
                    CategoryId = group.Key,
                    CategoryName = _context.Categories.Where(c => c.CategoryId == group.Key).Select(c => c.CategoryName).FirstOrDefault(),
                    TotalRevenue = group.Sum(oi => oi.TotalPrice) // Tổng doanh thu cho mỗi danh mục sản phẩm
                })
                .OrderByDescending(result => result.TotalRevenue) // Sắp xếp theo doanh thu giảm dần
                .ToListAsync();

            return Ok(revenueByCategory);
        }
        
        // Thống kê doanh thu theo danh mục sản phẩm trong khoảng thời gian
        [HttpGet("revenue-by-category-in-period-time")]
        public async Task<IActionResult> GetRevenueByCategory([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            var revenueByCategory = await _context.OrderItems
                .Where(oi => oi.Order.Status == "Completed"
                              && oi.Order.OrderDate >= startDate
                              && oi.Order.OrderDate <= endDate)
                .GroupBy(oi => oi.Product.CategoryId) // Nhóm theo danh mục sản phẩm
                .Select(group => new
                {
                    CategoryId = group.Key,
                    CategoryName = _context.Categories.Where(c => c.CategoryId == group.Key).Select(c => c.CategoryName).FirstOrDefault(),
                    TotalRevenue = group.Sum(oi => oi.TotalPrice) // Tổng doanh thu cho mỗi danh mục sản phẩm
                })
                .OrderByDescending(result => result.TotalRevenue) // Sắp xếp theo doanh thu giảm dần
                .ToListAsync();

            return Ok(revenueByCategory);
        }

        // Thống kê doanh thu theo sản phẩm
        [HttpGet("revenue-by-product")]
        public async Task<IActionResult> GetRevenueByProduct()
        {
            var revenueByProduct = await _context.OrderItems
                .Where(oi => oi.Order.Status == "Completed") // Chỉ tính các đơn hàng hoàn thành
                .GroupBy(oi => oi.ProductId)
                .Select(group => new
                {
                    ProductId = group.Key,
                    TotalRevenue = group.Sum(oi => oi.TotalPrice) // Tổng doanh thu cho mỗi sản phẩm
                })
                .OrderByDescending(result => result.TotalRevenue) // Sắp xếp theo doanh thu giảm dần
                .ToListAsync();

            return Ok(revenueByProduct);
        }

        // Thống kê doanh thu theo sản phẩm trong khoảng thời gian
        [HttpGet("revenue-by-product-in-period-time")]
        public async Task<IActionResult> GetRevenueByProduct([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            // Lọc các đơn hàng hoàn thành trong khoảng thời gian
            var revenueByProduct = await _context.OrderItems
                .Where(oi => oi.Order.Status == "Completed" && oi.Order.OrderDate >= startDate && oi.Order.OrderDate <= endDate)
                .GroupBy(oi => oi.ProductId)
                .Select(group => new
                {
                    ProductId = group.Key,
                    TotalRevenue = group.Sum(oi => oi.TotalPrice) // Tổng doanh thu cho mỗi sản phẩm
                })
                .OrderByDescending(result => result.TotalRevenue) // Sắp xếp theo doanh thu giảm dần
                .ToListAsync();

            return Ok(revenueByProduct);
        }

        // 2. Thống kê số đơn hàng
        [HttpGet("orders-count")]
        public async Task<IActionResult> GetOrderCount()
        {
            var orderCount = await _context.Orders.CountAsync();
            return Ok(orderCount);
        }

        [HttpGet("orders-count-by-time")]
        public async Task<IActionResult> GetOrderCountByTime([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
        {
            // Bắt đầu truy vấn với tất cả đơn hàng
            var query = _context.Orders.AsQueryable();

            // Lọc theo ngày bắt đầu nếu có
            if (startDate.HasValue)
            {
                query = query.Where(o => o.OrderDate >= startDate.Value);
            }

            // Lọc theo ngày kết thúc nếu có
            if (endDate.HasValue)
            {
                query = query.Where(o => o.OrderDate <= endDate.Value);
            }

            // Đếm số lượng đơn hàng
            var orderCount = await query.CountAsync();

            return Ok(orderCount);
        }

        // 3. Thống kê số người dùng mới trong tháng
        [HttpGet("new-users")]
        public async Task<IActionResult> GetNewUsers()
        {
            var startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1); // Đầu tháng
            var newUserCount = await _context.Users
                .Where(u => u.CreatedAt >= startDate) // Người dùng mới trong tháng
                .CountAsync();

            return Ok(newUserCount);
        }

        [HttpGet("new-users-by-year-group-by-quarter")]
        public async Task<IActionResult> GetNewUsersByYear([FromQuery] int? year)
        {
            // Nếu không có năm, sử dụng năm hiện tại
            int selectedYear = year ?? DateTime.Now.Year;

            // Tạo danh sách để lưu số người dùng mới theo từng quý
            var newUsersByQuarter = new Dictionary<int, int>();

            // Đối tượng chứa các quý và khoảng thời gian tương ứng
            var quarters = new[]
            {
                new { Quarter = 1, StartDate = new DateTime(selectedYear, 1, 1), EndDate = new DateTime(selectedYear, 3, 31) },
                new { Quarter = 2, StartDate = new DateTime(selectedYear, 4, 1), EndDate = new DateTime(selectedYear, 6, 30) },
                new { Quarter = 3, StartDate = new DateTime(selectedYear, 7, 1), EndDate = new DateTime(selectedYear, 9, 30) },
                new { Quarter = 4, StartDate = new DateTime(selectedYear, 10, 1), EndDate = new DateTime(selectedYear, 12, 31) }
            };

            // Lấy số người dùng mới cho từng quý
            foreach (var quarter in quarters)
            {
                var newUserCount = await _context.Users
                    .Where(u => u.CreatedAt >= quarter.StartDate && u.CreatedAt <= quarter.EndDate)
                    .CountAsync();

                newUsersByQuarter[quarter.Quarter] = newUserCount;
            }

            // Trả về số lượng người dùng mới cho từng quý
            return Ok(new
            {
                Year = selectedYear,
                Quarters = newUsersByQuarter
            });
        }

        // 4. Thống kê sản phẩm bán chạy
        [HttpGet("best-seller-products")]
        public async Task<IActionResult> GetBestSellerProducts()
        {
            var bestSellers = await _context.OrderItems
                .GroupBy(oi => oi.Product)
                .Select(group => new
                {
                    ProductId = group.Key.ProductId,
                    ProductName = group.Key.ProductName,
                    TotalSold = group.Sum(oi => oi.Quantity)
                })
                .OrderByDescending(g => g.TotalSold)
                .Take(5) // Lấy 5 sản phẩm bán chạy nhất
                .ToListAsync();

            return Ok(bestSellers);
        }

        [HttpGet("best-sellers-by-quarter")]
        public async Task<IActionResult> GetBestSellersByQuarter([FromQuery] int? year)
        {
            // Nếu không có năm, sử dụng năm hiện tại
            int selectedYear = year ?? DateTime.Now.Year;

            // Đối tượng chứa các quý và khoảng thời gian tương ứng
            var quarters = new[]
            {
                new { Quarter = 1, StartDate = new DateTime(selectedYear, 1, 1), EndDate = new DateTime(selectedYear, 3, 31) },
                new { Quarter = 2, StartDate = new DateTime(selectedYear, 4, 1), EndDate = new DateTime(selectedYear, 6, 30) },
                new { Quarter = 3, StartDate = new DateTime(selectedYear, 7, 1), EndDate = new DateTime(selectedYear, 9, 30) },
                new { Quarter = 4, StartDate = new DateTime(selectedYear, 10, 1), EndDate = new DateTime(selectedYear, 12, 31) }
            };

            // Tạo danh sách để lưu sản phẩm bán chạy theo từng quý
            var bestSellersByQuarter = new Dictionary<int, List<BestSellerDto>>();

            // Lấy sản phẩm bán chạy cho từng quý
            foreach (var quarter in quarters)
            {
                var bestSellers = await _context.OrderItems
                    .Where(oi => oi.Order.OrderDate >= quarter.StartDate && oi.Order.OrderDate <= quarter.EndDate)
                    .GroupBy(oi => oi.Product)
                    .Select(group => new BestSellerDto
                    {
                        ProductId = group.Key.ProductId,
                        ProductName = group.Key.ProductName,
                        TotalSold = group.Sum(oi => oi.Quantity)
                    })
                    .OrderByDescending(g => g.TotalSold)
                    .Take(5) // Lấy 5 sản phẩm bán chạy nhất
                    .ToListAsync();

                bestSellersByQuarter[quarter.Quarter] = bestSellers;
            }

            // Trả về sản phẩm bán chạy cho từng quý
            return Ok(new
            {
                Year = selectedYear,
                Quarters = bestSellersByQuarter
            });
        }
    }
    // DTO để chứa thông tin sản phẩm bán chạy
    public class BestSellerDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public int TotalSold { get; set; }

    }
}
