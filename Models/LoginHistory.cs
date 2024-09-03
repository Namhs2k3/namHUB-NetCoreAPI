using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

[Table("LoginHistory")]
public partial class LoginHistory
{
    [Key]
    [Column("login_id")]
    public int LoginId { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [Column("login_time", TypeName = "datetime")]
    public DateTime? LoginTime { get; set; }

    [Column("ip_address")]
    [StringLength(45)]
    [Unicode(false)]
    public string? IpAddress { get; set; }

    [Column("success")]
    public bool? Success { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("LoginHistories")]
    public virtual User? User { get; set; }
}
