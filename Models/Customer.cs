using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

[Index("Email", Name = "UQ__Customer__AB6E61645B14527A", IsUnique = true)]
public partial class Customer
{
    [Key]
    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("user_image")]
    public string? UserImageURL { get; set; } // Thêm thuộc tính mới

    [Column("full_name")]
    [StringLength(255)]
    public string FullName { get; set; } = null!;

    [Column("email")]
    [StringLength(100)]
    public string Email { get; set; } = null!;

    [Column("phone")]
    [StringLength(20)]
    public string? Phone { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "datetime")]
    public DateTime? UpdatedAt { get; set; }

    [InverseProperty("Customer")]
    public virtual ICollection<Address> Addresses { get; set; } = new List<Address>();

    [InverseProperty("Customer")]
    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    // Liên kết đến User
    [Column("user_id")]
    public int UserId { get; set; } // Khóa ngoại liên kết với bảng User

    [ForeignKey("UserId")]
    public virtual User? User { get; set; }
}
