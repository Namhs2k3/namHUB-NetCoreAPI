using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

public partial class DeliveryAssignment
{
    [Key]
    [Column("assignment_id")]
    public int AssignmentId { get; set; }

    [Column("order_id")]
    public int? OrderId { get; set; }

    [Column("delivery_person_id")]
    public int? DeliveryPersonId { get; set; }

    [Column("assigned_at", TypeName = "datetime")]
    public DateTime? AssignedAt { get; set; }

    [Column("status")]
    [StringLength(50)]
    public string? Status { get; set; }

    [ForeignKey("DeliveryPersonId")]
    [InverseProperty("DeliveryAssignments")]
    public virtual DeliveryPersonnel? DeliveryPerson { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("DeliveryAssignments")]
    public virtual Order? Order { get; set; }
}
