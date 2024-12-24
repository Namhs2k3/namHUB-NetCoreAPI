using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

[Table("ProductRating")]
public partial class ProductRating
{
    [Key]
    public int RatingId { get; set; }

    public int ProductId { get; set; }

    public int UserId { get; set; }

    public byte Rating { get; set; }

    public string? Comment { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("ProductRatings")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("ProductRatings")]
    public virtual User User { get; set; } = null!;
}
