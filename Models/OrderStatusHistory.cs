using System;
using System.Collections.Generic;

namespace namHub_FastFood.Models;

public partial class OrderStatusHistory
{
    public int StatusHistoryId { get; set; }

    public int? OrderId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? StatusDate { get; set; }

    public virtual Order? Order { get; set; }
}
