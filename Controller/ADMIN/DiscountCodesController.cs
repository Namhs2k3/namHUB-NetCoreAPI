using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using namHub_FastFood.Models;
using System;
using System.ComponentModel.DataAnnotations;

namespace namHub_FastFood.Controller.ADMIN
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiscountCodesController : ControllerBase
    {
        private readonly namHUBDbContext _context;

        public DiscountCodesController(namHUBDbContext context)
        {
            _context = context;
        }

        [HttpGet("get-discount-codes")]
        [Authorize(Roles = "ADMIN")] // Chỉ admin mới có quyền truy cập
        public async Task<IActionResult> GetDiscountCodes()
        {
            var discountCodes = await _context.DiscountCodes
                .Select(d => new
                {
                    d.DiscountId,
                    d.Code,
                    d.DiscountValue,
                    d.DiscountType,
                    d.MinOrderValue,
                    d.StartDate,
                    d.EndDate,
                    d.MaxUsageCount,
                    d.CurrentUsageCount,
                    d.IsSingleUse,
                    d.CreatedAt,
                    d.UpdatedAt,
                })
                .ToListAsync();

            if (discountCodes == null)
            {
                return NotFound("Không có mã giảm giá nào.");
            }

            return Ok(discountCodes);
        }

        // POST: api/DiscountCodes
        [HttpPost("create")]
        [Authorize(Roles = "ADMIN")] // Chỉ admin mới có quyền tạo mã giảm giá
        public async Task<IActionResult> CreateDiscountCode([FromBody] DiscountCodeDto discountCodeDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Kiểm tra xem mã giảm giá đã tồn tại chưa
            var existingCode = await _context.DiscountCodes
                .FirstOrDefaultAsync(dc => dc.Code == discountCodeDto.Code);

            if (existingCode != null)
            {
                return BadRequest("Mã giảm giá này đã tồn tại.");
            }

            // Tạo mã giảm giá mới
            var discountCode = new DiscountCode
            {
                Code = discountCodeDto.Code,
                DiscountValue = discountCodeDto.DiscountValue,
                DiscountType = discountCodeDto.discountType.ToLower(),
                MinOrderValue = discountCodeDto.MinimumOrderAmount,
                StartDate = discountCodeDto.StartDate,
                EndDate = discountCodeDto.EndDate,
                MaxUsageCount = discountCodeDto.MaxUsageCount,
                IsSingleUse = discountCodeDto.IsSingleUse,
                IsActive = discountCodeDto.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            _context.DiscountCodes.Add(discountCode);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Mã giảm giá đã được tạo thành công!", DiscountCode = discountCode });
        }

        [HttpPut("update/{discountCodeId}")]
        [Authorize(Roles = "ADMIN")] // Chỉ admin mới có quyền sửa mã giảm giá
        public async Task<IActionResult> UpdateDiscountCode(int discountCodeId, [FromBody] DiscountCodeDto discountCodeDto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Kiểm tra xem mã giảm giá có tồn tại không
            var existingDiscountCode = await _context.DiscountCodes.FindAsync(discountCodeId);
            if (existingDiscountCode == null)
            {
                return NotFound("Không tìm thấy mã giảm giá.");
            }

            // Cập nhật thông tin mã giảm giá
            existingDiscountCode.Code = discountCodeDto.Code;
            existingDiscountCode.DiscountValue = discountCodeDto.DiscountValue;
            existingDiscountCode.DiscountType = discountCodeDto.discountType.ToLower();
            existingDiscountCode.MinOrderValue = discountCodeDto.MinimumOrderAmount;
            existingDiscountCode.StartDate = discountCodeDto.StartDate;
            existingDiscountCode.EndDate = discountCodeDto.EndDate;
            existingDiscountCode.MaxUsageCount = discountCodeDto.MaxUsageCount;
            existingDiscountCode.IsSingleUse = discountCodeDto.IsSingleUse;
            existingDiscountCode.IsActive = discountCodeDto.IsActive;

            // Lưu thay đổi vào database
            _context.DiscountCodes.Update(existingDiscountCode);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "Cập nhật mã giảm giá thành công!", DiscountCode = existingDiscountCode });
        }
    }

    public class DiscountCodeDto
    {
        [Required]
        public string Code { get; set; }

        [Range(0, 100, ErrorMessage = "Giá trị giảm giá phải từ 0 đến 100")]
        public decimal DiscountValue { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Giá trị đơn hàng tối thiểu phải lớn hơn hoặc bằng 0")]
        public decimal? MinimumOrderAmount { get; set; }
        public string discountType { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số lần sử dụng tối đa phải lớn hơn 0")]
        public int MaxUsageCount { get; set; } = 1;

        public bool IsSingleUse { get; set; }
        public bool IsActive { get; set; }

    }


}
