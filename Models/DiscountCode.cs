using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

public partial class DiscountCode
{
    [Key]
    public int DiscountId { get; set; }

    [StringLength(50)]
    public string Code { get; set; } = null!;

    [Column(TypeName = "decimal(10, 2)")]
    public decimal DiscountValue { get; set; }

    [StringLength(10)]
    public string? DiscountType { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal? MinOrderValue { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime StartDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime EndDate { get; set; }

    public int? MaxUsageCount { get; set; }

    public bool IsSingleUse { get; set; }

    public bool IsActive { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedAt { get; set; }

    public int? CurrentUsageCount { get; set; }

    [InverseProperty("Discount")]
    public virtual ICollection<UsedDiscount> UsedDiscounts { get; set; } = new List<UsedDiscount>();
}
