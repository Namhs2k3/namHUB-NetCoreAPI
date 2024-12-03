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
        public async Task<IActionResult> GetProducts(int? id, string? name)
        {
            // Tạo URL đầy đủ (base URL + đường dẫn hình ảnh)
            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var products =  _context.Products.AsQueryable();

            if (id.HasValue && id.Value != 0)
            {
                products = products.Where(p => p.ProductId == id.Value);
            }
            if(!string.IsNullOrWhiteSpace(name))
            {
                products = products.Where(p => p.ProductName.Contains(name) || (p.Category != null && p.Category.CategoryName.Contains(name)));
            }
            var result = await products.Select(p => new //phải có từ khóa "new"
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
                p.DiscountedPrice,
                p.DiscountPercentage,
                p.Keywords
            })
                .ToListAsync();

            return Ok(result);
        }

        [HttpPost("add-product")]
        public async Task<IActionResult> AddProduct([FromForm] MyProduct myProduct)
        {
            // Kiểm tra dữ liệu đầu vào
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var duplicateProduct = await _context.Products
                                                    .AnyAsync(p => p.ProductName == myProduct.ProductName);
            if (duplicateProduct)
            {
                return BadRequest("Tên sản phẩm đã tồn tại!");
            }

            // Kiểm tra xem có file ảnh hay không
            if (myProduct.imgFile == null || myProduct.imgFile.Length == 0)
            {
                return BadRequest("Vui lòng chọn ảnh!");
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(myProduct.imgFile.FileName).ToLower();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest("Chỉ hỗ trợ các định dạng ảnh: .jpg, .jpeg, .png, .gif.");
            }

            // Lưu file ảnh
            var fileName = Path.GetFileName(myProduct.imgFile.FileName);
            var filePath = Path.Combine(_uploadFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await myProduct.imgFile.CopyToAsync(stream);
            }

            // Kiểm tra nếu giá sau giảm không được lớn hơn giá gốc
            if (myProduct.DiscountedPrice > myProduct.Price)
            {
                return BadRequest("Giá đã giảm không được lớn hơn giá gốc!");
            }

            decimal? discountPercentage = 0;
            if (myProduct.Price > 0)
            {
                // Tính phần trăm giảm giá nếu giá gốc và giá giảm khác nhau
                 discountPercentage = myProduct.DiscountedPrice < myProduct.Price
                    ? ((myProduct.Price - myProduct.DiscountedPrice) / myProduct.Price) * 100
                    : 0;
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
                DiscountedPrice = myProduct.DiscountedPrice ?? myProduct.Price,
                DiscountPercentage = discountPercentage
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

            // Kiểm tra nếu DiscountedPrice lớn hơn Price
            if (product.DiscountedPrice.HasValue && product.DiscountedPrice > product.Price)
            {
                return BadRequest("Giá đã giảm không được lớn hơn giá gốc");
            }

            var duplicateProduct = await _context.Products
                                                    .AnyAsync(p => p.ProductName == product.ProductName && p.ProductId != id);
            if (duplicateProduct)
            {
                return BadRequest("Tên sản phẩm đã tồn tại!");
            }


            // Kiểm tra xem có file ảnh mới hay không
            if (product.imgFile != null && product.imgFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(product.imgFile.FileName).ToLower();
                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest("Chỉ hỗ trợ các định dạng ảnh: .jpg, .jpeg, .png, .gif.");
                }
                // Lưu file ảnh
                var fileName = Path.GetFileName(product.imgFile.FileName);
                var filePath = Path.Combine(_uploadFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await product.imgFile.CopyToAsync(stream);
                }

                // Cập nhật ImageUrl nếu có ảnh mới
                existingProduct.ImageUrl = $"/images/{fileName}";
            }

            // Cập nhật các thuộc tính khác
            existingProduct.ProductName = product.ProductName;
            existingProduct.Description = product.Description;
            existingProduct.Price = product.Price;
            existingProduct.StockQuantity = product.StockQuantity;
            existingProduct.CategoryId = product.CategoryId;
            existingProduct.UpdatedAt = DateTime.Now;
            existingProduct.IsPopular = product.IsPopular;
            existingProduct.IsHidden = product.IsHidden;
            existingProduct.Keywords = product.keywords;

            // Cập nhật giá giảm và phần trăm giảm
            existingProduct.DiscountedPrice = product.DiscountedPrice ?? product.Price;
            if (product.Price > 0)
            {
                existingProduct.DiscountPercentage =
                    ((product.Price - existingProduct.DiscountedPrice) / product.Price) * 100;
            }
            else
            {
                existingProduct.DiscountPercentage = 0;
            }

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
        public decimal? DiscountedPrice { get; set; }

        public string? keywords { get; set; }
    }

}
