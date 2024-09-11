using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

[Table("OrderStatusHistory")]
public partial class OrderStatusHistory
{
    [Key]
    [Column("status_history_id")]
    public int StatusHistoryId { get; set; }

    [Column("order_id")]
    public int? OrderId { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string Status { get; set; } = null!;

    [Column("status_date", TypeName = "datetime")]
    public DateTime? StatusDate { get; set; }

    [Column("updated_by")]
    [StringLength(100)]
    public string? UpdatedBy { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("OrderStatusHistories")]
    public virtual Order? Order { get; set; }
}
