using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;

namespace namHub_FastFood.Controller.ADMIN
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "ADMIN")]
    public class BannersManageController : ControllerBase
    {
        public readonly namHUBDbContext _context;
        private readonly string _uploadFolder;
        public BannersManageController(namHUBDbContext dbContext)
        {
            _context = dbContext;

            // Đường dẫn thư mục upload hình ảnh (có thể là thư mục trong wwwroot)
            _uploadFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "Banner_Home");

            // Tạo thư mục nếu nó chưa tồn tại
            if (!Directory.Exists(_uploadFolder))
            {
                Directory.CreateDirectory(_uploadFolder);
            }
        }

        [HttpGet("get-banner-list-for-admin")]
        public async Task<IActionResult> GetBannerList()
        {
            // Tạo URL đầy đủ (base URL + đường dẫn hình ảnh)
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var banners = await _context.Banners
                .Select(b => new
                {
                    b.BannerId,
                    b.Title,
                    imgUrl = $"{baseUrl}{b.ImageUrl}",
                    b.Link,
                    b.DisplayOrder,
                    b.IsActive,
                    b.CreatedAt,
                    b.UpdatedAt,
                }).OrderBy(b => b.DisplayOrder).ToListAsync();

            return Ok(banners);
        }

        [HttpPost("add-banner")]
        public async Task<IActionResult> AddBanner([FromForm]BannerDto banner)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            // Kiểm tra xem có file ảnh hay không
            if (banner.imgFile == null || banner.imgFile.Length == 0)
            {
                return BadRequest("Chưa Có Ảnh Banner!");
            }

            // Lưu file ảnh
            var fileName = Path.GetFileName(banner.imgFile.FileName);
            var filePath = Path.Combine(_uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await banner.imgFile.CopyToAsync(stream);
            }

            var exitingBannerDisplayOrder = await _context.Banners.Select(d => d.DisplayOrder).ToListAsync();
            foreach (var bannerOrder in exitingBannerDisplayOrder)
            {
                if (banner.DisplayOrder < 0 || banner.DisplayOrder ==  bannerOrder) 
                {
                    return BadRequest("Thứ tự ko hợp lệ hoặc đã tồn tại!");
                }
            }

            var newBanner = new Banner() 
            {
                Title = banner.Title,
                ImageUrl = $"/images/Banner_Home/{fileName}",
                DisplayOrder = banner.DisplayOrder,
                Link = banner.Link,
                IsActive = banner.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _context.Banners.Add(newBanner);
            await _context.SaveChangesAsync();

            return Ok(newBanner);
        }

        [HttpPut("update-banner/{id}")]
        public async Task<IActionResult> UpdateBanner(int id, [FromForm] BannerDto banner)
        {
            // Tìm banner hiện tại theo ID
            var existingBanner = await _context.Banners.FindAsync(id);
            if (existingBanner == null)
            {
                return NotFound("Banner không tồn tại.");
            }

            // Kiểm tra dữ liệu đầu vào
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Nếu có file ảnh mới
            if (banner.imgFile != null && banner.imgFile.Length > 0)
            {
                // Xóa file ảnh cũ nếu có
                if (!string.IsNullOrEmpty(existingBanner.ImageUrl))
                {
                    var oldFilePath = Path.Combine(_uploadFolder, Path.GetFileName(existingBanner.ImageUrl));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // Lưu file ảnh mới
                var fileName = Path.GetFileName(banner.imgFile.FileName);
                var filePath = Path.Combine(_uploadFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await banner.imgFile.CopyToAsync(stream);
                }

                existingBanner.ImageUrl = $"/images/Banner_Home/{fileName}";
            }

            // Kiểm tra thứ tự hiển thị mới
            var existingBannerOrders = await _context.Banners
                .Where(b => b.BannerId != id)
                .Select(b => b.DisplayOrder)
                .ToListAsync();

            if (banner.DisplayOrder < 0 || existingBannerOrders.Contains(banner.DisplayOrder))
            {
                return BadRequest("Thứ tự không hợp lệ hoặc đã tồn tại.");
            }

            // Cập nhật thông tin banner
            existingBanner.Title = banner.Title;
            existingBanner.DisplayOrder = banner.DisplayOrder;
            existingBanner.Link = banner.Link;
            existingBanner.IsActive = banner.IsActive;
            existingBanner.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(existingBanner);
        }

        [HttpDelete("delete-banner/{id}")]
        public async Task<IActionResult> DeleteBanner(int id)
        {
            // Tìm banner hiện tại theo ID
            var banner = await _context.Banners.FindAsync(id);
            if (banner == null)
            {
                return NotFound("Banner không tồn tại.");
            }

            // Xóa file ảnh liên quan nếu có
            if (!string.IsNullOrEmpty(banner.ImageUrl))
            {
                var filePath = Path.Combine(_uploadFolder, Path.GetFileName(banner.ImageUrl));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            // Xóa banner khỏi cơ sở dữ liệu
            _context.Banners.Remove(banner);
            await _context.SaveChangesAsync();

            return Ok("Banner đã được xóa thành công.");
        }


    }

    public class BannerDto
    {
        public string Title { get; set; }
        public IFormFile imgFile { get; set; }
        public string? Link { get; set; }
        public int? DisplayOrder { get; set; }
        public bool? IsActive { get; set; }
    }
}
