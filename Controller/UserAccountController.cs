﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;

namespace namHub_FastFood.Controller
{
    [Route( "api/user-account" )]
    [ApiController]
    public class UserAccountController : ControllerBase
    {
        private readonly namHUBDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<UserAccountController> _logger;
        private readonly IConfiguration _configure;

        public UserAccountController( namHUBDbContext context, IEmailService emailService, ILogger<UserAccountController> logger, IConfiguration configure )
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _configure = configure;
        }

        [HttpPost( "register" )]
        public async Task<IActionResult> Register( UserRegisterRequest request )
        {
            // Kiểm tra tính duy nhất của tên người dùng và email
            if ( await _context.Users.AnyAsync( u => u.Username == request.Username ) )
                return BadRequest( "Tên người dùng đã tồn tại." );
            if ( await _context.Users.AnyAsync( u => u.Email == request.Email ) )
                return BadRequest( "Email đã tồn tại." );

            // Tạo salt và hash cho mật khẩu
            var salt = GenerateSalt();
            var passwordHash = ComputeHash( request.Password, salt );

            // Tạo mã xác thực email
            var emailVerificationCode = Guid.NewGuid().ToString( "N" );

            // Tạo đối tượng người dùng mới
            var user = new User
            {
                Username = request.Username,
                PasswordHash = Convert.ToBase64String( passwordHash ),
                Salt = salt,
                Email = request.Email,
                FullName = request.FullName,
                CreatedAt = DateTime.UtcNow.AddHours( 7 ),
                UpdatedAt = DateTime.UtcNow.AddHours( 7 ),
                EmailVerified = false, // Đặt trạng thái xác thực email là false
                EmailVerificationCode = emailVerificationCode.Trim() // Lưu mã xác thực email
            };

            // Lưu người dùng vào cơ sở dữ liệu
            _context.Users.Add( user );
            await _context.SaveChangesAsync();

            // Tìm role mặc định USER
            var userRole = await _context.Roles.FirstOrDefaultAsync( r => r.RoleName == "USER" );
            if ( userRole != null )
            {
                // Gán role "USER" cho người dùng
                _context.UserRoles.Add( new UserRole { UserId = user.UserId, RoleId = userRole.RoleId } );
                await _context.SaveChangesAsync();
            }

            // Tạo liên kết xác thực email
            var verificationLink = Url.Action(
                "VerifyEmail",
                "UserAccount",
                new { userId = user.UserId, code = emailVerificationCode },
                Request.Scheme
            );

            // Gửi email xác thực
            await _emailService.SendEmailAsync(
                user.Email,
                "Xác thực email",
                $"Vui lòng xác thực email của bạn bằng cách nhấp vào nút dưới đây: ",
                verificationLink
            );

            return Ok( "Đăng ký thành công! Vui lòng kiểm tra email của bạn để xác thực tài khoản." );
        }

        [HttpGet( "verify-email" )]
        public async Task<IActionResult> VerifyEmail( int userId, string code )
        {
            var user = await _context.Users.FindAsync( userId );

            // Log giá trị để kiểm tra
            Console.WriteLine( $"User ID: {userId}, Code: {code}, Stored Code: {user?.EmailVerificationCode}, User : {user}" );

            if ( user == null )
            {
                var message = Uri.EscapeDataString( "Không Tìm Thấy Người Dùng!" );
                return Redirect( $"{_configure["FrontEndUrl"]}/verify-email-failure?message={message}" );
            }

            if ( string.IsNullOrWhiteSpace( user.EmailVerificationCode ) )
            {
                var message = Uri.EscapeDataString( "Mã Xác Thực Không Hợp Lệ Hoặc Đã Được Sử Dụng!" );
                return Redirect( $"{_configure["FrontEndUrl"]}/verify-email-failure?message={message}" );
            }

            if ( user.EmailVerificationCode.Trim() != code )
            {
                var message = Uri.EscapeDataString( "Link không còn hiệu lực nữa, hãy dùng link mới nhất!" );
                return Redirect( $"{_configure["FrontEndUrl"]}/verify-email-failure?message={message}" );
            }

            user.EmailVerified = true;
            user.EmailVerificationCode = null; // Xóa mã xác thực sau khi đã xác thực
            await _context.SaveChangesAsync();

            var successMessage = Uri.EscapeDataString( "Email được xác thực thành công, bạn có thể đăng nhập được rồi!" );
            return Redirect( $"{_configure["FrontEndUrl"]}/verify-email-success?message={successMessage}" );
        }

        [HttpPost( "forgot-password" )]
        public async Task<IActionResult> ForgotPassword( ForgotPasswordRequest request )
        {
            var user = await _context.Users.SingleOrDefaultAsync( u => u.Email == request.Email );
            if ( user == null )
            {
                return BadRequest( "Email không tồn tại." );
            }

            // Tạo mã xác thực
            var resetToken = Guid.NewGuid().ToString( "N" );
            var expiresAt = DateTime.UtcNow.AddHours( 7 ).AddHours( 1 ); // Thay đổi thời gian hết hạn theo yêu cầu

            // Lưu mã xác thực vào cơ sở dữ liệu
            var resetPasswordToken = new ResetPasswordToken
            {
                UserId = user.UserId,
                Token = resetToken,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow.AddHours( 7 )
            };
            _context.ResetPasswordTokens.Add( resetPasswordToken );
            await _context.SaveChangesAsync();

            // Gửi email với mã xác thực
            //var resetLink = Url.Action("ResetPassword", "User", new { token = resetToken }, Request.Scheme); đoạn mã này chỉ phù hợp nếu có làm View của Action "ResetPassWord"
            await _emailService.SendEmailAsync( request.Email, "Đặt lại mật khẩu", $"Token để đặt lại mật khẩu của bạn là : {resetToken}!\n Vui lòng không chia sẻ cho bất kì ai để đảm bảo tài khoản của bạn được an toàn! \n Love You Pặc Pặc!!! <3" );

            return Ok( "Mã đặt lại mật khẩu đã được gửi đến email của bạn." );
        }

        // Có thể ko cần vì làm FE React riêng
        [HttpGet( "reset-password" )]
        public IActionResult ResetPassword( string token )
        {
            // Hiển thị giao diện đặt lại mật khẩu với token (trong trường hợp dùng giao diện web)
            return Ok();
        }

        [HttpPost( "reset-password" )]
        public async Task<IActionResult> ResetPassword( ResetPasswordRequest request )
        {
            // Kiểm tra tính hợp lệ của mật khẩu mới
            if ( string.IsNullOrWhiteSpace( request.NewPassword ) || request.NewPassword.Length < 8 )
            {
                return BadRequest( "Mật khẩu mới không hợp lệ. Vui lòng nhập mật khẩu có ít nhất 8 ký tự." );
            }

            var authToken = await _context.ResetPasswordTokens
                .Where( t => t.Token == request.Token && t.ExpiresAt > DateTime.UtcNow.AddHours( 7 ) )
                .OrderByDescending( t => t.ExpiresAt )
                .FirstOrDefaultAsync();

            var authTokenList = new List<ResetPasswordToken>();
            if ( authToken == null )
            {
                return BadRequest( "Mã xác thực không hợp lệ hoặc đã hết hạn." );
            }
            else
            {
                authTokenList = await _context.ResetPasswordTokens.Where( t => t.UserId == authToken.UserId )
                .ToListAsync();
            }

            var user = await _context.Users.FindAsync( authToken.UserId );
            if ( user == null )
            {
                return BadRequest( "Người dùng không tồn tại." );
            }

            // Tạo salt và hash cho mật khẩu mới
            var salt = GenerateSalt();
            var passwordHash = ComputeHash( request.NewPassword, salt );

            // Cập nhật mật khẩu người dùng
            // Ko cần dùng đến method Update mà chỉ cần thay đổi bằng giá trị mới và lưu vào csdl thôi
            user.PasswordHash = Convert.ToBase64String( passwordHash );
            user.Salt = salt;
            // Lưu MK rồi mới xóa mã xác thực để tránh rủi ro (ko thay đổi đc MK mà đã xóa mã)
            await _context.SaveChangesAsync();

            // Xóa mã xác thực sau khi đặt lại mật khẩu thành công
            if ( authTokenList.Count == 1 )
            {
                _context.ResetPasswordTokens.Remove( authToken );
            }
            else if ( authTokenList.Count > 1 )
            {
                _context.ResetPasswordTokens.RemoveRange( authTokenList );
            }
            await _context.SaveChangesAsync();

            return Ok( "Mật khẩu đã được đặt lại thành công." );
        }

        // Phương thức tạo salt ngẫu nhiên
        private string GenerateSalt()
        {
            var saltBytes = new byte[16];
            using ( var rng = new System.Security.Cryptography.RNGCryptoServiceProvider() )
            {
                rng.GetBytes( saltBytes );
            }
            return Convert.ToBase64String( saltBytes );
        }

        // Phương thức mã hóa mật khẩu với salt
        private byte[] ComputeHash( string password, string salt )
        {
            using ( var hmac = new System.Security.Cryptography.HMACSHA256( Encoding.UTF8.GetBytes( salt ) ) )
            {
                return hmac.ComputeHash( Encoding.UTF8.GetBytes( password ) );
            }
        }

        // Logout
        [HttpPost( "log-out" )]
        public async Task<IActionResult> Logout()
        {
            // Lấy thông tin người dùng từ HTTPcontext
            // User ở Claim Principal chứ ko phải ở db
            // còn user_id là claim của JWT được Nam tạo trong phương thức Login
            var userId = User.FindFirst( "user_id" )?.Value;
            Console.WriteLine( $"Đây là User Id {userId}" );
            if ( userId == null )
            {
                return BadRequest( "Người dùng chưa đăng nhập!" );
            }

            // Xóa cookie
            Response.Cookies.Delete( "jwt" );

            return Ok( "Đăng xuất thành công!" );
        }
    }

    public class UserRegisterRequest
    {
        [Required]
        public string Username { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

        public string FullName { get; set; }
    }

    public class ForgotPasswordRequest
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        [Required]
        public string Token { get; set; }

        [Required]
        [DataType( DataType.Password )]
        public string NewPassword { get; set; }
    }
}