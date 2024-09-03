using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

public partial class AuthenticationToken
{
    [Key]
    [Column("token_id")]
    public int TokenId { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("token")]
    [StringLength(64)]
    [Unicode(false)]
    public string Token { get; set; } = null!;

    [Column("expires_at", TypeName = "datetime")]
    public DateTime ExpiresAt { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("AuthenticationTokens")]
    public virtual User? User { get; set; }
}
