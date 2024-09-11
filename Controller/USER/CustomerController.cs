using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
                    ImgURL = $"{baseUrl}{c.imgURL}",
                    Description = c.Description,
                })
                .ToListAsync();

            return Ok(categories);
        }

        [Authorize(Roles = "ADMIN,EMPLOYEE,DELIVER,USER")]
        [HttpGet("get-info")]
        public async Task<IActionResult> GetCusInfo()
        {
            // Lấy thông tin customer id từ claim của token
            var userIdClaim = User.FindFirst("user_id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("Hãy đăng nhập để xem thông tin!");
            }

            int userId;
            if (!int.TryParse(userIdClaim, out userId))
            {
                return BadRequest("Không thể lấy thông tin khách hàng!");
            }

            // Tìm khách hàng và tải trước các địa chỉ của họ
            var customer = await _context.Customers
                .Include(c => c.Addresses) // Tải trước các địa chỉ của khách hàng
                .Include(c => c.User) // phải tải trước tt từ User, nếu ko sẽ bị 'null'
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null)
            {
                return NotFound("Không tìm thấy khách hàng.");
            }

            // Lấy địa chỉ mặc định nếu có
            var defaultAddress = customer.Addresses
                .FirstOrDefault(a => a.IsDefault == true);

            // Tạo đối tượng thông tin khách hàng
            var cusInfo = new
            {
                customer?.CustomerId,
                UserName = customer?.User?.Username,
                customer?.FullName,
                customer?.Phone,
                customer?.Email,
                customer?.CreatedAt,
                customer?.UserImageURL,
                DefaultAddress = defaultAddress != null
                    ? $"{defaultAddress.AddressLine1}, {defaultAddress.City}"
                    : "Không có địa chỉ mặc định",
                customer?.UpdatedAt,
            };

            return Ok(cusInfo);
        }

        [Authorize(Roles = "ADMIN,EMPLOYEE,DELIVER,USER")]
        [HttpGet("get-customer-addresses")]
        public async Task<IActionResult> GetCusAddr()
        {
            // Lấy thông tin customer id từ claim của token
            var userIdClaim = User.FindFirst("user_id")?.Value;

            if (string.IsNullOrEmpty(userIdClaim))
            {
                return Unauthorized("Hãy đăng nhập để xem thông tin!");
            }

            int userId;
            if (!int.TryParse(userIdClaim, out userId))
            {
                return BadRequest("Không thể lấy thông tin khách hàng!");
            }

            // Tìm khách hàng và tải trước danh sách địa chỉ của họ
            var customer = await _context.Customers
                .Include(c => c.Addresses) // Tải trước các địa chỉ của khách hàng
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null)
            {
                return NotFound("Không tìm thấy khách hàng.");
            }

            var customerAddresses = customer.Addresses.Select(address => new
            {
                AddressId = address.AddressId,
                AddressLine1 = address.AddressLine1,
                AddressLine2 = address.AddressLine2,
                City = address.City,
                State = address.State,
                IsDefault = address.IsDefault,
                PostalCode = address.PostalCode,
                Country = address.Country
            }).ToList();

            return Ok(customerAddresses);
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

        // Add khi người dùng mua hàng hoặc thiết lập thông tin khởi đầu
        [Authorize(Roles = "ADMIN,EMPLOYEE,DELIVER,USER")]
        [HttpPost("add-info")]
        public async Task<IActionResult> AddCusInfo([FromForm] UpdateCustomerInfoDto model)
        {
            // Lấy thông tin customer id từ claim của token
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return Unauthorized("Hãy đăng nhập để thực hiện cập nhật thông tin!");
            }

            // Kiểm tra dữ liệu đầu vào
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Kiểm tra xem khách hàng đã tồn tại hay chưa
            var existingCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId.Value);
            if (existingCustomer != null)
            {
                return BadRequest("Khách hàng đã có thông tin, hãy chọn 'Cập Nhật Thông Tin'!");
            }

            // Kiểm tra xem có file ảnh hay không
            if (model.UserImageURL == null || model.UserImageURL.Length == 0)
            {
                return BadRequest("Chưa tải ảnh nào hết!");
            }

            // Lưu file ảnh
            var fileName = Path.GetFileName(model.UserImageURL.FileName);
            var filePath = Path.Combine(_uploadFolder, fileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.UserImageURL.CopyToAsync(stream);
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Lỗi khi lưu hình ảnh: {ex.Message}");
            }

            // Tìm người dùng và cập nhật thông tin
            var exitingUser = await _context.Users.FindAsync(userId.Value);
            if (exitingUser == null)
            {
                return NotFound("Người dùng ko tồn tại!");
            }
            exitingUser.UpdatedAt = DateTime.UtcNow;
            exitingUser.FullName = model.FullName;

            // Lưu cập nhật người dùng
            await _context.SaveChangesAsync();

            // Thêm mới khách hàng
            var newCustomer = new Customer()
            {
                FullName = model.FullName,
                Email = exitingUser.Email,
                Phone = model.Phone,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UserId = userId.Value,
                UserImageURL = $"/image/{fileName}",
            };

            _context.Customers.Add(newCustomer);
            await _context.SaveChangesAsync();

            return Ok("Thêm mới thông tin thành công.");
        }


        // Update khi người dùng muốn thay đổi thông tin
        [Authorize(Roles = "ADMIN,EMPLOYEE,DELIVER,USER")]
        [HttpPut("update-info")]
        public async Task<IActionResult> UpdateCusInfo([FromForm] UpdateCustomerInfoDto model)
        {
            // Lấy thông tin customer id từ claim của token
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return Unauthorized("Hãy đăng nhập để thực hiện cập nhật thông tin!");
            }

            // Tìm khách hàng
            var customer = await _context.Customers
                .Include(c => c.Addresses) // Tải trước các địa chỉ của khách hàng
                .FirstOrDefaultAsync(c => c.UserId == userId.Value);

            if (customer == null)
            {
                return NotFound("Không tìm thấy khách hàng.");
            }

            var exitingUser = await _context.Users.FindAsync(userId.Value);
            if (exitingUser == null)
            {
                return NotFound("Người dùng ko tồn tại!");
            }

            // Cập nhật thông tin cho người dùng
            exitingUser.UpdatedAt = DateTime.UtcNow;
            exitingUser.FullName = model.FullName ?? exitingUser.FullName;
            exitingUser.Email = model.Email ?? exitingUser.Email;

            // Lưu thay đổi thông tin người dùng
            await _context.SaveChangesAsync();

            // Kiểm tra dữ liệu đầu vào
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Kiểm tra xem có file ảnh hay không
            if (model.UserImageURL != null && model.UserImageURL.Length > 0)
            {
                var fileName = Path.GetFileName(model.UserImageURL.FileName);
                var filePath = Path.Combine(_uploadFolder, fileName);

                try
                {
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.UserImageURL.CopyToAsync(stream);
                    }
                    customer.UserImageURL = $"/image/{fileName}";
                }
                catch (Exception ex)
                {
                    return StatusCode(500, $"Lỗi khi lưu hình ảnh: {ex.Message}");
                }
            }

            // Cập nhật thông tin khách hàng
            customer.FullName = model.FullName ?? customer.FullName;
            customer.Phone = model.Phone ?? customer.Phone;
            customer.Email = model.Email ?? customer.Email;
            customer.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok("Cập nhật thông tin thành công.");
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

    public class UpdateCustomerInfoDto
    {
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public IFormFile? UserImageURL { get; set; }
    }
}
