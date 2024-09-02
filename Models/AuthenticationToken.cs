using System;
using System.Collections.Generic;

namespace namHub_FastFood.Models;

public partial class AuthenticationToken
{
    public int TokenId { get; set; }

    public int? UserId { get; set; }

    public string Token { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
