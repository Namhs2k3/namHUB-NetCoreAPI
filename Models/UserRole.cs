using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

[PrimaryKey("UserId", "RoleId")]
public partial class UserRole
{
    [Key]
    [Column("user_id")]
    public int UserId { get; set; }

    [Key]
    [Column("role_id")]
    public int RoleId { get; set; }

    [Column("assigned_at", TypeName = "datetime")]
    public DateTime? AssignedAt { get; set; }

    [ForeignKey("RoleId")]
    [InverseProperty("UserRoles")]
    public virtual Role Role { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("UserRoles")]
    public virtual User User { get; set; } = null!;
}
