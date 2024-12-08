using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

[Table("Banner")]
public partial class Banner
{
    [Key]
    [Column("banner_id")]
    public int BannerId { get; set; }

    [Column("title")]
    [StringLength(255)]
    public string Title { get; set; } = null!;

    [Column("image_url")]
    [StringLength(255)]
    public string ImageUrl { get; set; } = null!;

    [Column("link")]
    [StringLength(255)]
    public string? Link { get; set; }

    [Column("display_order")]
    public int? DisplayOrder { get; set; }

    [Column("is_active")]
    public bool? IsActive { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "datetime")]
    public DateTime? UpdatedAt { get; set; }

    [Column("startDate", TypeName = "datetime")]
    public DateTime StartDate { get; set; }

    [Column("endDate", TypeName = "datetime")]
    public DateTime EndDate { get; set; }
}
