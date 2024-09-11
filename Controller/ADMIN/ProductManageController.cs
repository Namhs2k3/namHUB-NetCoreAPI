using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;

namespace namHub_FastFood.Controller.ADMIN
{
    [Route("api/product-manage-for-admin")]
    [ApiController]
    [Authorize(Roles = "ADMIN")]
    public class ProductManageController : ControllerBase
    {
        private readonly namHUBDbContext _context;
        private readonly string _uploadFolder;
        public ProductManageController(namHUBDbContext dbContext)
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
        [HttpGet("get-products-list")]
        public async Task<IActionResult> GetProducts()
        {
            // Tạo URL đầy đủ (base URL + đường dẫn hình ảnh)
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var products = await _context.Products
                .Select(p => new //phải có từ khóa "new"
                {
                    p.ProductId,
                    p.ProductName,
                    p.Description,
                    p.Price,
                    p.StockQuantity,
                    p.CategoryId,
                    CategoryName = p.Category.CategoryName,
                    ImgURL = $"{baseUrl}{p.ImageUrl}", // Đảm bảo trả về URL hình ảnh
                    p.IsHidden,
                    p.IsPopular,
                })
                .ToListAsync();

            return Ok(products);
        }

        [HttpPost("add-product")]
        public async Task<IActionResult> AddProduct([FromForm] MyProduct myProduct)
        {
            // Kiểm tra dữ liệu đầu vào
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Kiểm tra xem có file ảnh hay không
            if (myProduct.imgFile == null || myProduct.imgFile.Length == 0)
            {
                return BadRequest("No image uploaded.");
            }

            // Lưu file ảnh
            var fileName = Path.GetFileName(myProduct.imgFile.FileName);
            var filePath = Path.Combine(_uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await myProduct.imgFile.CopyToAsync(stream);
            }

            var product = new Product()
            {
                ProductName = myProduct.ProductName,
                Price = myProduct.Price,
                Description = myProduct.Description,
                IsHidden = myProduct.IsHidden,
                StockQuantity = myProduct.StockQuantity,
                CategoryId = myProduct.CategoryId,
                ImageUrl = $"/images/{fileName}",
                IsPopular = myProduct.IsPopular,
            };
            _context.Products.Add(product);
            await _context.SaveChangesAsync();
            return Ok(product);
        }
        // Sửa sản phẩm
        [HttpPut("update-product/{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromForm] MyProduct product)
        {
            var existingProduct = await _context.Products.FindAsync(id);
            if (existingProduct == null) return NotFound();

            // Kiểm tra dữ liệu đầu vào
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Kiểm tra xem có file ảnh hay không
            if (product.imgFile == null || product.imgFile.Length == 0)
            {
                return BadRequest("No image uploaded.");
            }

            // Lưu file ảnh
            var fileName = Path.GetFileName(product.imgFile.FileName);
            var filePath = Path.Combine(_uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await product.imgFile.CopyToAsync(stream);
            }

            existingProduct.ProductName = product.ProductName;
            existingProduct.Description = product.Description;
            existingProduct.Price = product.Price;
            existingProduct.StockQuantity = product.StockQuantity;
            existingProduct.CategoryId = product.CategoryId;
            existingProduct.ImageUrl = $"/images/{fileName}";
            existingProduct.UpdatedAt = DateTime.Now;
            existingProduct.IsPopular = product.IsPopular;
            existingProduct.IsHidden = product.IsHidden;

            await _context.SaveChangesAsync();
            return Ok(existingProduct);
        }
    }
    public class MyProduct
    {
        public string ProductName { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public bool IsHidden { get; set; } // Cờ để ẩn sản phẩm
        public bool IsPopular { get; set; }
        public int StockQuantity { get; set; }
        public int CategoryId { get; set; }
        public IFormFile? imgFile { get; set; }
    }

}
