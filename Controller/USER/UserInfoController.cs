﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;

namespace namHub_FastFood.Controller.USER
{
    [Route("api/user-info")]
    [ApiController]
    public class UserInfoController : ControllerBase
    {
        public readonly namHUBDbContext _context;
        private readonly string _uploadFolder;
        public UserInfoController(namHUBDbContext dbContext)
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
                customer?.UserImage,
                DefaultAddress = defaultAddress != null
                    ? $"{defaultAddress.AddressLine1}, {defaultAddress.City}"
                    : "Không có địa chỉ mặc định",
                customer?.UpdatedAt,
            };

            return Ok(cusInfo);
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
                UserImage = $"/images/{fileName}",
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
            // Chỉ cần gọi 1 lần ở cuối phương thức là đc rồi, để đảm bảo hiệu suất tốt hơn
            // Nhưng như thế này cũng đc, chỉ là hiệu suất ko tốt bằng và ko toàn vẹn (lỡ cái này lưu đc nhưng ở dưới ko đc)
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
                    customer.UserImage = $"/images/{fileName}";
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

        [Authorize(Roles = "ADMIN,EMPLOYEE,DELIVER,USER")]
        [HttpGet("get-user-addresses")]
        public async Task<IActionResult> GetUserAddr()
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

        [Authorize]
        [HttpPost("add-user-address")]
        public async Task<IActionResult> AddUserAddress([FromForm]AddressDto userAddress)
        {
            var userId = GetUserIdFromClaims();
            if(userId == null)
            {
                return BadRequest("Vui lòng đăng nhập để xem thông tin!");
            }

            // Kiểm tra dữ liệu đầu vào
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Tìm customer dựa trên userId
            var customer = await _context.Customers
                .Include(c => c.Addresses) // phải eager load lên, nếu ko sẽ null
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (customer == null)
            {
                return NotFound("Không tìm thấy khách hàng!");
            }

            // Nếu địa chỉ mới là địa chỉ mặc định, cập nhật các địa chỉ khác của người dùng
            if (userAddress.IsDefault == true)
            {
                foreach (var address in customer.Addresses)
                {
                    if(address.IsDefault != false)
                    {
                        address.IsDefault = false;
                    }
                }
            }

            var newUserAddress = new Address()
            {
                CustomerId = customer.CustomerId,
                AddressLine1 = userAddress.AddressLine1,
                AddressLine2 = userAddress.AddressLine2 ?? "",
                City = userAddress.City,
                State = userAddress.State,
                IsDefault = userAddress.IsDefault,
                PostalCode = userAddress.PostalCode,
                Country = userAddress.Country,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _context.Addresses.Add(newUserAddress);
            await _context.SaveChangesAsync();
            
            return Ok(newUserAddress);
        }

        [HttpPut("update-user-address/{addressId}")]
        [Authorize]
        public async Task<IActionResult> UpdateUserAddr(int addressId, [FromForm] AddressDto userAddress)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return BadRequest("Vui lòng đăng nhập để tiếp tục");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var customer = await _context.Customers
                .Include(c => c.Addresses) // phải eager load lên, nếu ko sẽ null
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (customer == null)
            {
                return NotFound("Người dùng ko tồn tại!");
            }

            if(userAddress.IsDefault == true)
            {
                foreach(var address in customer.Addresses)
                {
                    if (address.IsDefault != false)
                    {
                        address.IsDefault = false;
                    }
                }
            }
            var exitingAddresses = customer.Addresses
                .FirstOrDefault(a => a.AddressId == addressId);

            if (exitingAddresses == null)
            {
                return NotFound("Địa chỉ ko tồn tại!");
            }

            exitingAddresses.UpdatedAt = DateTime.UtcNow;
            exitingAddresses.AddressLine1 = userAddress.AddressLine1;
            exitingAddresses.AddressLine2 = userAddress.AddressLine2 ?? "";
            exitingAddresses.City = userAddress.City;
            exitingAddresses.State = userAddress.State;
            exitingAddresses.Country = userAddress.Country;
            exitingAddresses.IsDefault = userAddress.IsDefault ?? exitingAddresses.IsDefault;
            exitingAddresses.PostalCode = userAddress.PostalCode ?? "";

            await _context.SaveChangesAsync();

            return Ok(exitingAddresses);

        }

        [HttpDelete("delete-user-address/{addressId}")]
        [Authorize]
        public async Task<IActionResult> DelUserAddress(int addressId)
        {
            var userId = GetUserIdFromClaims();
            if (userId == null)
            {
                return BadRequest("Vui lòng đăng nhập để tiếp tục");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var customer = await _context.Customers
                .Include(c => c.Addresses) // phải eager load lên, nếu ko sẽ null
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (customer == null)
            {
                return NotFound("Người dùng ko tồn tại!");
            }

            var exitingAddresses = customer.Addresses
                .FirstOrDefault(a => a.AddressId == addressId);

            if (exitingAddresses == null)
            {
                return NotFound("Địa chỉ ko tồn tại!");
            }

            _context.Addresses.Remove(exitingAddresses);
            await _context.SaveChangesAsync();

            return Ok("Xóa địa chỉ thành công!");
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

    public class AddressDto
    {
        public string AddressLine1 { get; set; } = null!;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = null!;
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string Country { get; set; } = null!;
        public bool? IsDefault { get; set; }
    }
}
