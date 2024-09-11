using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;

namespace namHub_FastFood.Controller.ADMIN
{
    [Route("api/users-account-manage-for-admin")]
    [ApiController]
    [Authorize(Roles = "ADMIN")]
    public class UsersManageController : ControllerBase
    {
        private readonly namHUBDbContext _context;
        public UsersManageController(namHUBDbContext context)
        {
            _context = context;
        }

        [HttpGet("get-list-user-account")]
        public async Task<IActionResult> GetUA()
        {
            var userAccounts = await _context.Users
                .Include(u => u.UserRoles) // Tải trước UserRoles
                .ThenInclude(ur => ur.Role) // Tải trước Role thông qua UserRoles
                .Select(u => new
                {
                    u.UserId,
                    u.Username,
                    u.Email,
                    u.FullName,
                    u.EmailVerified,
                    Roles = u.UserRoles.Select(ur => new
                    {
                        RoleId = ur.RoleId,
                        RoleName = ur.Role != null ? ur.Role.RoleName : "No Role"
                    }).ToList(), // Lấy tất cả các role
                    u.CreatedAt,
                    u.UpdatedAt,
                })
                .ToListAsync();

            return Ok(userAccounts);
        }
        // Đoạn code này thực hiện chức năng cập nhật role cho người dùng nếu ng dùng chỉ có 1 role
        [HttpPost("update-user-role/{id}")]
        public async Task<IActionResult> UpdateUA(int id, [FromForm] string newRole)
        {
            // Tìm người dùng
            var user = await _context.Users.Include(u => u.UserRoles)
                                           .FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null)
            {
                return BadRequest("User id not found!");
            }

            // Tìm vai trò mới
            var newRoleEntity = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == newRole);
            if (newRoleEntity == null)
            {
                return BadRequest("Role not found!");
            }

            // Tìm vai trò hiện tại của người dùng
            var userRole = user.UserRoles.FirstOrDefault();
            if (userRole != null)
            {
                // Xóa vai trò hiện tại của người dùng
                _context.UserRoles.Remove(userRole);
            }

            // Thêm vai trò mới
            var newUserRole = new UserRole
            {
                UserId = id,
                RoleId = newRoleEntity.RoleId
            };

            _context.UserRoles.Add(newUserRole);
            await _context.SaveChangesAsync();

            return Ok("User role updated successfully.");
        }



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
        public DateTime? UpdateAt { get; set;}

    }
}
