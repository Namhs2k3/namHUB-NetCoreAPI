using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;
using System.Text;

namespace namHub_FastFood.Controller.ADMIN
{
    [Route( "api/users-account-manage-for-admin" )]
    [ApiController]
    [Authorize( Roles = "ADMIN" )]
    public class UsersManageController : ControllerBase
    {
        private readonly namHUBDbContext _context;
        private readonly IEmailService _emailService;

        public UsersManageController( namHUBDbContext context, IEmailService emailService )
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet( "get-list-user-account" )]
        public async Task<IActionResult> GetUA()
        {
            var userAccounts = await _context.Users
                .Include( u => u.UserRoles ) // Tải trước UserRoles
                .ThenInclude( ur => ur.Role ) // Tải trước Role thông qua UserRoles
                .Select( u => new
                {
                    u.UserId,
                    u.Username,
                    u.Email,
                    u.FullName,
                    u.EmailVerified,
                    Roles = u.UserRoles.Select( ur => new
                    {
                        RoleId = ur.RoleId,
                        RoleName = ur.Role != null ? ur.Role.RoleName : "No Role"
                    } ).ToList(), // Lấy tất cả các role
                    u.CreatedAt,
                    u.UpdatedAt,
                } )
                .ToListAsync();

            return Ok( userAccounts );
        }

        [HttpPost( "add-employee" )]
        public async Task<IActionResult> AddEmployee( AddUserAccountDTO newEmployee )
        {
            if ( !ModelState.IsValid )
            {
                return BadRequest( ModelState );
            }

            var employeeRole = await _context.Roles.FirstOrDefaultAsync( r => r.RoleName == "EMPLOYEE" );
            if ( employeeRole == null )
            {
                return BadRequest( "Vai trò không tồn tại." );
            }

            var existingUserName = await _context.Users.FirstOrDefaultAsync( u => u.Username == newEmployee.Username );
            if ( existingUserName != null )
            {
                return BadRequest( "Tên Người Dùng Đã Tồn Tại" );
            }

            var existingUserEmail = await _context.Users.FirstOrDefaultAsync( u => u.Email == newEmployee.Email );
            if ( existingUserEmail != null )
            {
                return BadRequest( "Email Này Đã Được Đăng Ký Bởi Tài Khoản Khác!" );
            }

            // Tạo salt và hash cho mật khẩu
            var salt = GenerateSalt();
            var passwordHash = ComputeHash( newEmployee.Password, salt );
            // Tạo mã xác thực email
            var emailVerificationCode = Guid.NewGuid().ToString( "N" );

            var newEmp = new User
            {
                Username = newEmployee.Username,
                FullName = newEmployee.FullName,
                PasswordHash = Convert.ToBase64String( passwordHash ),
                Email = newEmployee.Email, // email có thể là do công ty cấp hoặc nhân viên đưa mail cho admin để tạo tài khoản
                Salt = salt,
                EmailVerified = false,
                CreatedAt = DateTime.UtcNow.AddHours(7),
                UpdatedAt = DateTime.UtcNow.AddHours(7),
                EmailVerificationCode = emailVerificationCode
            };

            // Thêm vai trò cho user
            newEmp.UserRoles = new List<UserRole>
            {
                new UserRole { RoleId = employeeRole.RoleId } // UserId sẽ được thiết lập tự động sau khi lưu vì CÓ Liên Kết Khóa Ngoại đến bảng User (newEmp)
            };

            _context.Add( newEmp );
            await _context.SaveChangesAsync();

            // Tạo liên kết xác thực email
            var verificationLink = Url.Action(
                "VerifyEmail",
                "UserAccount",
                new { userId = newEmp.UserId, code = emailVerificationCode },
                Request.Scheme
            );

            // Gửi email xác thực
            await _emailService.SendEmailAsync(
                newEmployee.Email,
                "Xác thực email",
                $"Vui lòng xác thực email của bạn bằng cách nhấp vào nút dưới đây: ",
                verificationLink
            );

            return Ok( new { message = "Thêm Nhân Viên Mới Thành Công! ", data = newEmp } );
        }

        // Đoạn code này thực hiện chức năng cập nhật roles cho người dùng
        [HttpPut( "update-user-role/{id}" )]
        public async Task<IActionResult> UpdateUA( int id, [FromBody] List<string> newRoles )
        {
            // Tìm người dùng
            var user = await _context.Users.Include( u => u.UserRoles )
                                           .FirstOrDefaultAsync( u => u.UserId == id );
            if ( user == null )
            {
                return BadRequest( "User id not found!" );
            }

            // Lấy danh sách các vai trò từ tên vai trò
            var newRoleEntities = await _context.Roles
                                                .Where( r => newRoles.Contains( r.RoleName ) )
                                                .ToListAsync();
            if ( newRoleEntities.Count != newRoles.Count )
            {
                return BadRequest( "Some roles were not found!" );
            }

            // Lấy danh sách các RoleId mới từ danh sách vai trò mới
            var newRoleIds = newRoleEntities.Select( r => r.RoleId ).ToHashSet();

            // Lọc và xóa các vai trò cũ không còn trong danh sách mới
            var rolesToRemove = user.UserRoles.Where( ur => !newRoleIds.Contains( ur.RoleId ) ).ToList();
            _context.UserRoles.RemoveRange( rolesToRemove );

            // Thêm các vai trò mới chưa có
            foreach ( var newRoleId in newRoleIds )
            {
                if ( !user.UserRoles.Any( ur => ur.RoleId == newRoleId ) )
                {
                    var newUserRole = new UserRole
                    {
                        UserId = id,
                        RoleId = newRoleId
                    };
                    _context.UserRoles.Add( newUserRole );
                }
            }
            user.UpdatedAt = DateTime.UtcNow.AddHours(7);
            // Lưu thay đổi vào cơ sở dữ liệu
            await _context.SaveChangesAsync();

            return Ok( "User roles updated successfully." );
        }

        [HttpDelete( "delete-employee/{id}" )]
        public async Task<IActionResult> DeleteEmployee( int id )
        {
            var existingEmployee = await _context.Users
                                                 .Include( u => u.UserRoles ) // Bao gồm các quan hệ cần xử lý
                                                 .FirstOrDefaultAsync( u => u.UserId == id );

            if ( existingEmployee == null )
            {
                return NotFound( new { message = "Không tìm thấy nhân viên này!" } );
            }

            // Nếu cần xử lý các quan hệ liên quan thủ công
            _context.UserRoles.RemoveRange( existingEmployee.UserRoles ); // Xóa UserRoles liên quan

            _context.Users.Remove( existingEmployee ); // Xóa User
            await _context.SaveChangesAsync();

            return Ok( new { message = "Đã xóa nhân viên thành công!" } );
        }

        private string GenerateSalt()
        {
            var saltBytes = new byte[16];
            using ( var rng = new System.Security.Cryptography.RNGCryptoServiceProvider() )
            {
                rng.GetBytes( saltBytes );
            }
            return Convert.ToBase64String( saltBytes );
        }

        private byte[] ComputeHash( string password, string salt )
        {
            using ( var hmac = new System.Security.Cryptography.HMACSHA256( Encoding.UTF8.GetBytes( salt ) ) )
            {
                return hmac.ComputeHash( Encoding.UTF8.GetBytes( password ) );
            }
        }
    }

    public class AddUserAccountDTO
    {
        public string Username { get; set; } = null!;
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? Password { get; set; }
    }

    public class UserAccountDTO
    {
        public string Username { get; set; } = null!;
        public string? Email { get; set; }
        public string? FullName { get; set; }
        public string? EmailVerified { get; set; }
        public string? RoleId { get; set; }
        public string? RoleName { get; set; }
        public DateTime? CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }
    }
}