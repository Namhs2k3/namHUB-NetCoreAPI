using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using namHub_FastFood.Models;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace namHub_FastFood.Controller
{
    [Route("api/user-login")]
    [ApiController]
    public class LoginController : ControllerBase
    {
        private readonly namHUBDbContext _context;
        private readonly IConfiguration _configuration;

        public LoginController(namHUBDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null || !VerifyPassword(request.Password, user.PasswordHash, user.Salt))
            {
                return Unauthorized();
            }

            if (!user.EmailVerified)
            {
                return Unauthorized("Email not verified. Please check your email.");
            }

            // Lấy roles của user từ Bảng UserRoles
            var roles = await _context.UserRoles
                .Where(ur => ur.UserId == user.UserId)
                .Select(ur => ur.Role.RoleName)
                .ToListAsync();

            var claims = new List<Claim>()
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim("user_id", user.UserId.ToString())
            };

            // Thêm các role vào claims
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(7).AddMinutes(60),
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            // Set cookie
            Response.Cookies.Append("jwt", tokenString, new CookieOptions
            {
                HttpOnly = true,
                Expires = DateTime.UtcNow.AddHours(7).AddMinutes(60)
            });

            var refreshToken = new RefreshToken
            {
                Token = GenerateRefreshToken(),
                Expires = DateTime.UtcNow.AddHours(7).AddDays(7), // Refresh token có hiệu lực 7 ngày
                IsUsed = false,
                IsRevoked = false,
                UserId = user.UserId
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();
            await RemoveUOERefreshToken(user.UserId);


            return Ok(new { 
                token = tokenString,
                refreshToken = refreshToken.Token
            });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var storedToken = await _context.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == request.Token);

            if (storedToken == null || storedToken.IsUsed || storedToken.IsRevoked || storedToken.Expires < DateTime.UtcNow.AddHours(7))
            {
                return Unauthorized("Refresh Token không hợp lệ hoặc đã hết hạn sử dụng!");
            }

            // Đánh dấu refresh token này là đã sử dụng
            storedToken.IsUsed = true;
            await _context.SaveChangesAsync();

            // Tạo access token mới
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, storedToken.User.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, storedToken.User.Username),
                new Claim("user_id", storedToken.User.UserId.ToString())
            };

            // Thêm roles nếu cần
            var roles = await _context.UserRoles
                .Where(ur => ur.UserId == storedToken.User.UserId)
                .Select(ur => ur.Role.RoleName)
                .ToListAsync();

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var newToken = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(7).AddMinutes(60),
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(newToken);

            // Tạo refresh token mới
            var newRefreshToken = new RefreshToken
            {
                Token = GenerateRefreshToken(),
                Expires = DateTime.UtcNow.AddHours(7).AddDays(7),
                IsUsed = false,
                IsRevoked = false,
                UserId = storedToken.UserId
            };

            _context.RefreshTokens.Add(newRefreshToken);
            await _context.SaveChangesAsync();
            await RemoveUOERefreshToken(storedToken.User.UserId);

            return Ok(new
            {
                token = tokenString,
                refreshToken = newRefreshToken.Token
            });
        }

        private bool VerifyPassword(string password, string storedHash, string storedSalt)
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(storedSalt)))
            {
                var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(password));
                // Giải mã storedHash từ base64 thành mảng byte
                var storedHashBytes = Convert.FromBase64String(storedHash);

                // So sánh hai mảng byte
                return storedHashBytes.SequenceEqual(computedHash);
            }
        }

        private string GenerateRefreshToken()
        {
            var randomBytes = new byte[32];
            RandomNumberGenerator.Fill(randomBytes); // Phương thức mới để tạo số ngẫu nhiên
            return Convert.ToBase64String(randomBytes);
        }

        [HttpDelete("/delete-rft")]
        public async Task<IActionResult> RemoveUOERefreshToken(int userId)
        {
            if (userId == null)
            {
                return BadRequest("Vui lòng đăng nhập để tiếp tục");
            }

            try
            {
                // Xóa các refresh token đã hết hạn hoặc đã sử dụng cho user hiện tại
                await _context.RefreshTokens
                    .Where(rf => rf.UserId == userId && (rf.Expires < DateTime.UtcNow.AddHours(7) || rf.IsUsed == true))
                    .ExecuteDeleteAsync(); // Nếu dùng EF Core 7.0 trở lên

                return Ok("Đã Dọn Dẹp Refresh Token!");
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Có lỗi xảy ra khi dọn dẹp refresh token");
            }
        }

    }
    public class RefreshTokenRequest
    {
        public string Token { get; set; }
    }

    public class UserLoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
