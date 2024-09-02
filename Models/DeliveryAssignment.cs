using System;
using System.Collections.Generic;

namespace namHub_FastFood.Models;

public partial class DeliveryAssignment
{
    public int AssignmentId { get; set; }

    public int? OrderId { get; set; }

    public int? DeliveryPersonId { get; set; }

    public DateTime? AssignedAt { get; set; }

    public string? Status { get; set; }

    public virtual DeliveryPersonnel? DeliveryPerson { get; set; }

    public virtual Order? Order { get; set; }
}
