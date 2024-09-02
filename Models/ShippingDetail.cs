using System;
using System.Collections.Generic;

namespace namHub_FastFood.Models;

public partial class ShippingDetail
{
    public int ShippingId { get; set; }

    public int? OrderId { get; set; }

    public string? ShippingMethod { get; set; }

    public string? TrackingNumber { get; set; }

    public DateTime? ShippingDate { get; set; }

    public DateTime? DeliveryDate { get; set; }

    public virtual Order? Order { get; set; }
}
