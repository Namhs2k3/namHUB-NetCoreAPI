using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

[Index("Email", Name = "UQ__Users__AB6E6164599F608B", IsUnique = true)]
[Index("Username", Name = "UQ__Users__F3DBC5723279775B", IsUnique = true)]
public partial class User
{
    [Key]
    [Column("user_id")]
    public int UserId { get; set; }

    [Column("username")]
    [StringLength(50)]
    [Unicode(false)]
    public string Username { get; set; } = null!;

    [Column("password_hash")]
    [StringLength(64)]
    [Unicode(false)]
    public string PasswordHash { get; set; } = null!;

    [Column("salt")]
    [StringLength(256)]
    public string? Salt { get; set; }

    [Column("email")]
    [StringLength(100)]
    [Unicode(false)]
    public string Email { get; set; } = null!;

    [Column("full_name")]
    [StringLength(100)]
    [Unicode(false)]
    public string? FullName { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "datetime")]
    public DateTime? UpdatedAt { get; set; }

    [Column("email_verified")]
    public bool EmailVerified { get; set; }

    [Column("email_verification_code")]
    [StringLength(64)]
    [Unicode(false)]
    public string? EmailVerificationCode { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<AuthenticationToken> AuthenticationTokens { get; set; } = new List<AuthenticationToken>();

    [InverseProperty("User")]
    public virtual ICollection<Cart> Carts { get; set; } = new List<Cart>();

    [InverseProperty("User")]
    public virtual ICollection<LoginHistory> LoginHistories { get; set; } = new List<LoginHistory>();

    [InverseProperty("User")]
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    // Liên kết với Customer
    public virtual Customer? Customer { get; set; }
}
