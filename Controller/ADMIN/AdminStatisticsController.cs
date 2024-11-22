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
            var totalRevenue = await _context.Payments
                .Where(o => o.PaymentStatus == "Completed") // Chỉ tính các đơn hàng hoàn thành
                .SumAsync(o => o.Amount);       // Tổng doanh thu từ các đơn hàng

            return Ok(totalRevenue);
        }

        // Thống kê doanh thu theo tháng, quý, năm sử dụng OrderDate và Status
        [HttpGet("revenue-by-time")]
        public async Task<IActionResult> GetRevenueByTime([FromQuery] int? year, [FromQuery] int? month, [FromQuery] int? quarter)
        {
            var query = _context.Payments
                .Where(o => o.PaymentStatus == "Completed") // Chỉ lấy các đơn hàng đã hoàn thành
                .AsQueryable();

            // Lọc theo năm nếu có
            if (year.HasValue)
            {
                query = query.Where(o => o.PaymentDate.Value.Year == year.Value);
            }

            // Lọc theo tháng nếu có
            if (month.HasValue)
            {
                query = query.Where(o => o.PaymentDate.Value.Month == month.Value);
            }

            // Lọc theo quý nếu có
            if (quarter.HasValue)
            {
                query = query.Where(o => (o.PaymentDate.Value.Month - 1) / 3 + 1 == quarter.Value);
            }

            var totalRevenue = await query
                .SumAsync(o => o.Amount);

            return Ok(totalRevenue);
        }

        // Thống kê doanh thu theo khoảng thời gian sử dụng Status và OrderDate
        [HttpGet("revenue-by-date")]
        public async Task<IActionResult> GetRevenueByDate(DateTime startDate, DateTime endDate)
        {
            // Đảm bảo endDate không bao gồm ngày cuối cùng, nên thêm một ngày vào endDate để bao gồm toàn bộ ngày kết thúc
            endDate = endDate.Date.AddDays(1).AddTicks(-1);
            var totalRevenue = await _context.Payments
                .Where(o => o.PaymentStatus == "Completed" && o.PaymentDate >= startDate && o.PaymentDate <= endDate)
                .SumAsync(o => o.Amount);

            return Ok(totalRevenue);
        }

        // Thống kê doanh thu theo phương thức thanh toán
        [HttpGet("revenue-by-payment-method")]
        public async Task<IActionResult> GetRevenueByPaymentMethod(string paymentMethod)
        {
            var totalRevenue = await _context.Payments
                .Where(o => o.PaymentStatus == "Completed" && o.PaymentMethod == paymentMethod)
                .SumAsync(o => o.Amount);

            return Ok(totalRevenue);
        }

        // Thống kê doanh thu theo phương thức thanh toán trong khoảng thời gian
        [HttpGet("revenue-by-payment-method-in-period-time")]
        public async Task<IActionResult> GetRevenueByPaymentMethod([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            // Lọc các đơn hàng hoàn thành trong khoảng thời gian
            var revenueByPaymentMethod = await _context.Payments
                .Where(p => p.PaymentStatus == "Completed" && p.PaymentDate >= startDate && p.PaymentDate <= endDate.Date.AddDays(1).AddTicks(-1))
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
            // Bước 1: Lấy dữ liệu OrderItems và thông tin giảm giá
            var filteredData = await _context.OrderItems
                .Where(oi => oi.Product != null &&
                             oi.Product.Category != null &&
                             oi.Order.Payments.Any(p => p.PaymentStatus == "Completed"))
                .Select(oi => new
                {
                    CategoryId = oi.Product.CategoryId,
                    CategoryName = oi.Product.Category.CategoryName,
                    ProductPrice = oi.Product.Price, // Giá sản phẩm gốc
                    TotalAmount = oi.TotalPrice, // Tổng giá trị hóa đơn chưa giảm giá
                    OrderTotal = oi.Order.TotalAmount, // Tổng giá trị hóa đơn (cả giảm giá)
                    DiscountAmount = oi.Order.DiscountAmount // Mức giảm giá của hóa đơn
                })
                .ToListAsync();

            // Bước 2: Tính tổng giá trị của hóa đơn và phân bổ giảm giá
            var revenueByCategory = filteredData
                .GroupBy(item => new { item.CategoryId, item.CategoryName })
                .Select(group => new
                {
                    CategoryId = group.Key.CategoryId,
                    CategoryName = group.Key.CategoryName,
                    TotalRevenue = group.Sum(item =>
                        item.TotalAmount -
                        ((item.TotalAmount / item.OrderTotal) * item.DiscountAmount)) // Áp dụng phân bổ giảm giá cho từng OrderItem
                })
                .OrderByDescending(result => result.TotalRevenue)
                .ToList();

            return Ok(revenueByCategory);
        }





        // Thống kê doanh thu theo danh mục sản phẩm trong khoảng thời gian
        [HttpGet("revenue-by-category-in-period-time")]
        public async Task<IActionResult> GetRevenueByCategory([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            // Bước 1: Lấy dữ liệu OrderItems và thông tin giảm giá
            var filteredData = await _context.OrderItems
                .Where(oi => oi.Product != null &&
                             oi.Product.Category != null &&
                             oi.Order.Payments.Any(p => p.PaymentStatus == "Completed" && p.PaymentDate >= startDate && p.PaymentDate <= endDate.Date.AddDays(1).AddTicks(-1)))
                .Select(oi => new
                {
                    CategoryId = oi.Product.CategoryId,
                    CategoryName = oi.Product.Category.CategoryName,
                    ProductPrice = oi.Product.Price, // Giá sản phẩm gốc
                    TotalAmount = oi.TotalPrice, // Tổng giá trị hóa đơn chưa giảm giá
                    OrderTotal = oi.Order.TotalAmount, // Tổng giá trị hóa đơn (cả giảm giá)
                    DiscountAmount = oi.Order.DiscountAmount // Mức giảm giá của hóa đơn
                })
                .ToListAsync();

            // Bước 2: Tính tổng giá trị của hóa đơn và phân bổ giảm giá
            var revenueByCategory = filteredData
                .GroupBy(item => new { item.CategoryId, item.CategoryName })
                .Select(group => new
                {
                    CategoryId = group.Key.CategoryId,
                    CategoryName = group.Key.CategoryName,
                    TotalRevenue = group.Sum(item =>
                        item.TotalAmount -
                        ((item.TotalAmount / item.OrderTotal) * item.DiscountAmount)) // Áp dụng phân bổ giảm giá cho từng OrderItem
                })
                .OrderByDescending(result => result.TotalRevenue)
                .ToList();

            return Ok(revenueByCategory);
        }

        // Thống kê doanh thu theo sản phẩm
        [HttpGet("revenue-by-product")]
        public async Task<IActionResult> GetRevenueByProduct()
        {
            // Bước 1: Lấy dữ liệu OrderItems và thông tin giảm giá
            var filteredData = await _context.OrderItems
                .Where(oi => oi.Product != null &&
                             oi.Order.Payments.Any(p => p.PaymentStatus == "Completed"))
                .Select(oi => new
                {
                    ProdId = oi.Product.ProductId,
                    ProdName = oi.Product.ProductName,
                    ProductPrice = oi.Product.Price, // Giá sản phẩm gốc
                    TotalAmount = oi.TotalPrice, // Tổng giá trị hóa đơn chưa giảm giá
                    OrderTotal = oi.Order.TotalAmount, // Tổng giá trị hóa đơn (cả giảm giá)
                    DiscountAmount = oi.Order.DiscountAmount // Mức giảm giá của hóa đơn
                })
                .ToListAsync();

            // Bước 2: Tính tổng giá trị của hóa đơn và phân bổ giảm giá
            var revenueByProduct = filteredData
                .GroupBy(item => new { item.ProdId, item.ProdName })
                .Select(group => new
                {
                    ProductId = group.Key.ProdId,
                    ProductName = group.Key.ProdName,
                    TotalRevenue = group.Sum(item =>
                        item.TotalAmount -
                        ((item.TotalAmount / item.OrderTotal) * item.DiscountAmount)) // Áp dụng phân bổ giảm giá cho từng OrderItem
                })
                .OrderByDescending(result => result.TotalRevenue)
                .ToList();

            return Ok(revenueByProduct);
        }

        // Thống kê doanh thu theo sản phẩm trong khoảng thời gian
        [HttpGet("revenue-by-product-in-period-time")]
        public async Task<IActionResult> GetRevenueByProduct([FromQuery] DateTime startDate, [FromQuery] DateTime endDate)
        {
            // Bước 1: Lấy dữ liệu OrderItems và thông tin giảm giá
            var filteredData = await _context.OrderItems
                .Where(oi => oi.Product != null &&
                             oi.Order.Payments.Any(p => p.PaymentStatus == "Completed" && p.PaymentDate >= startDate && p.PaymentDate <= endDate.Date.AddDays(1).AddTicks(-1)))
                .Select(oi => new
                {
                    ProdId = oi.Product.ProductId,
                    ProdName = oi.Product.ProductName,
                    ProductPrice = oi.Product.Price, // Giá sản phẩm gốc
                    TotalAmount = oi.TotalPrice, // Tổng giá trị hóa đơn chưa giảm giá
                    OrderTotal = oi.Order.TotalAmount, // Tổng giá trị hóa đơn (cả giảm giá)
                    DiscountAmount = oi.Order.DiscountAmount // Mức giảm giá của hóa đơn
                })
                .ToListAsync();

            // Bước 2: Tính tổng giá trị của hóa đơn và phân bổ giảm giá
            var revenueByProduct = filteredData
                .GroupBy(item => new { item.ProdId, item.ProdName })
                .Select(group => new
                {
                    ProductId = group.Key.ProdId,
                    ProductName = group.Key.ProdName,
                    TotalRevenue = group.Sum(item =>
                        item.TotalAmount -
                        ((item.TotalAmount / item.OrderTotal) * item.DiscountAmount)) // Áp dụng phân bổ giảm giá cho từng OrderItem
                })
                .OrderByDescending(result => result.TotalRevenue)
                .ToList();

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
                query = query.Where(o => o.OrderDate <= endDate.Value.Date.AddDays(1).AddTicks(-1));
            }

            // Đếm số lượng đơn hàng
            var orderCount = await query.CountAsync();

            return Ok(orderCount);
        }

        // 3. Thống kê số người dùng mới trong tháng
        [HttpGet("new-users")]
        public async Task<IActionResult> GetNewUsers(int year, int month)
        {
            var startDate = new DateTime(year, month, 1); // Đầu tháng
            var endDate = startDate.AddMonths(1); // Bắt đầu ngày đầu tiên của tháng tiếp theo

            var newUserCount = await _context.Users
                .Where(u => u.CreatedAt >= startDate && u.CreatedAt < endDate) // Người dùng mới trong tháng
                .CountAsync();

            return Ok(newUserCount);
        }

        [HttpGet("all-users")]
        public async Task<IActionResult> GetAllUsers()
        {
            var allUserCount = await _context.Users
                .CountAsync();

            return Ok(allUserCount);
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
            // Sử dụng năm hiện tại nếu không truyền vào
            int selectedYear = year ?? DateTime.Now.Year;

            // Xác định các quý
            var quarters = Enumerable.Range(1, 4).Select(q => new
            {
                Quarter = q,
                StartDate = new DateTime(selectedYear, (q - 1) * 3 + 1, 1),
                EndDate = new DateTime(selectedYear, q * 3, DateTime.DaysInMonth(selectedYear, q * 3))
            });

            // Tạo dictionary để lưu dữ liệu sản phẩm bán chạy theo quý
            var bestSellersByQuarter = new Dictionary<int, List<BestSellerDto>>();

            foreach (var quarter in quarters)
            {
                // Lấy danh sách sản phẩm bán chạy trong khoảng thời gian của quý
                var bestSellers = await _context.OrderItems
                    .Where(oi => oi.Order.Payments
                        .Any(p => p.PaymentStatus == "Completed" &&
                                  p.PaymentDate >= quarter.StartDate &&
                                  p.PaymentDate <= quarter.EndDate))
                    .GroupBy(oi => oi.Product)
                    .Select(group => new BestSellerDto
                    {
                        ProductId = group.Key.ProductId,
                        ProductName = group.Key.ProductName,
                        TotalSold = group.Sum(oi => oi.Quantity)
                    })
                    .OrderByDescending(g => g.TotalSold)
                    .Take(5)
                    .ToListAsync();

                bestSellersByQuarter[quarter.Quarter] = bestSellers;
            }

            // Trả về kết quả
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
