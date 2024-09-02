using System;
using System.Collections.Generic;

namespace namHub_FastFood.Models;

public partial class LoginHistory
{
    public int LoginId { get; set; }

    public int? UserId { get; set; }

    public DateTime? LoginTime { get; set; }

    public string? IpAddress { get; set; }

    public bool? Success { get; set; }

    public virtual User? User { get; set; }
}
