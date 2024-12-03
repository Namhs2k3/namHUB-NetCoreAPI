using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

public partial class RefreshToken
{
    [Key]
    public int Id { get; set; }

    [StringLength(512)]
    public string Token { get; set; } = null!;

    [Column(TypeName = "datetime")]
    public DateTime Expires { get; set; }

    public bool IsRevoked { get; set; }

    public bool IsUsed { get; set; }

    public int UserId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("RefreshTokens")]
    public virtual User User { get; set; } = null!;
}
