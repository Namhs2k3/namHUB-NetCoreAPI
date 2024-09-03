using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

[Index("PermissionName", Name = "UQ__Permissi__81C0F5A26355B120", IsUnique = true)]
public partial class Permission
{
    [Key]
    [Column("permission_id")]
    public int PermissionId { get; set; }

    [Column("permission_name")]
    [StringLength(50)]
    [Unicode(false)]
    public string PermissionName { get; set; } = null!;

    [Column("description")]
    public string? Description { get; set; }
}
