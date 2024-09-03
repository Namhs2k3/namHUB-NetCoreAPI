using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

public partial class ShippingDetail
{
    [Key]
    [Column("shipping_id")]
    public int ShippingId { get; set; }

    [Column("order_id")]
    public int? OrderId { get; set; }

    [Column("shipping_method")]
    [StringLength(100)]
    public string? ShippingMethod { get; set; }

    [Column("tracking_number")]
    [StringLength(100)]
    public string? TrackingNumber { get; set; }

    [Column("shipping_date", TypeName = "datetime")]
    public DateTime? ShippingDate { get; set; }

    [Column("delivery_date", TypeName = "datetime")]
    public DateTime? DeliveryDate { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("ShippingDetails")]
    public virtual Order? Order { get; set; }
}
