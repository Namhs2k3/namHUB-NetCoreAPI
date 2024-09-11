﻿using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;
using System.ComponentModel.DataAnnotations;

namespace namHub_FastFood.Controller.ADMIN
{
    [Route("api/categories-manage-for-admin")]
    [ApiController]
    [Authorize(Roles = "ADMIN")]
    public class CategoriesController : ControllerBase
    {

        private readonly namHUBDbContext _context;

        private readonly string _uploadFolder;
        public CategoriesController(namHUBDbContext dbContext)
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

        // Tính năng thêm danh mục
        [HttpPost("add-category")]
        public async Task<IActionResult> AddCategory([FromForm] MyCategory myCategory)
        {
            // Kiểm tra dữ liệu đầu vào
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Kiểm tra xem có file ảnh hay không
            if (myCategory.imgFile == null || myCategory.imgFile.Length == 0)
            {
                return BadRequest("No image uploaded.");
            }

            // Lưu file ảnh
            var fileName = Path.GetFileName(myCategory.imgFile.FileName);
            var filePath = Path.Combine(_uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await myCategory.imgFile.CopyToAsync(stream);
            }

            // Tạo đối tượng Category mới
            var category = new Category()
            {
                CategoryName = myCategory.CategoryName,
                Description = myCategory.CategoryDescription,
                imgURL = $"/images/{fileName}"
            };

            // Thêm vào cơ sở dữ liệu
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            // Trả về thông tin danh mục mới được thêm
            return Ok(category);
        }

        // Tính năng Sửa danh mục 
        [HttpPut("edit-category/{id}")] // Thêm {id} vào đường dẫn để đảm bảo rõ ràng hơn
        public async Task<IActionResult> Edit(int id, [FromForm] MyCategory myCategory)
        {
            // Tìm danh mục theo id
            var category = await _context.Categories.FindAsync(id);
            if (category == null)
            {
                return NotFound(); // Trả về lỗi 404 nếu danh mục không tồn tại
            }

            // Kiểm tra dữ liệu đầu vào
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState); // Trả về lỗi 400 nếu dữ liệu không hợp lệ
            }

            // Kiểm tra xem có file ảnh hay không
            if (myCategory.imgFile == null || myCategory.imgFile.Length == 0)
            {
                return BadRequest("No image uploaded.");
            }

            // Lưu file ảnh
            var fileName = Path.GetFileName(myCategory.imgFile.FileName);
            var filePath = Path.Combine(_uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await myCategory.imgFile.CopyToAsync(stream);
            }

            // Cập nhật thuộc tính của danh mục
            category.CategoryName = myCategory.CategoryName;
            category.Description = myCategory.CategoryDescription;
            category.imgURL = $"/images/{fileName}";
            category.UpdatedAt = DateTime.Now; // Cập nhật thời gian sửa đổi

            // Lưu thay đổi vào cơ sở dữ liệu
            await _context.SaveChangesAsync();

            // Trả về danh mục đã được cập nhật
            return Ok(category);
        }


    }
    public class MyCategory
    {
        [Required(ErrorMessage = "CategoryName is required.")]
        public string CategoryName { get; set; } = string.Empty;

        public string? CategoryDescription { get; set; } // Có thể là null

        public IFormFile? imgFile { get; set; } // Thêm cột imgURL
    }

}
