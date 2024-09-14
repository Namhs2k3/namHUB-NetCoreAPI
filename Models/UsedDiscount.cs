using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

public partial class UsedDiscount
{
    [Key]
    public int Id { get; set; }

    [Column("DiscountID")]
    public int DiscountId { get; set; }

    public int CustomerId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime UsedAt { get; set; }

    [ForeignKey("CustomerId")]
    [InverseProperty("UsedDiscounts")]
    public virtual Customer Customer { get; set; } = null!;

    [ForeignKey("DiscountId")]
    [InverseProperty("UsedDiscounts")]
    public virtual DiscountCode Discount { get; set; } = null!;
}
