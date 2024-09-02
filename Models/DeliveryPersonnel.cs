using System;
using System.Collections.Generic;

namespace namHub_FastFood.Models;

public partial class DeliveryPersonnel
{
    public int DeliveryPersonId { get; set; }

    public string FullName { get; set; } = null!;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? AssignedArea { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<DeliveryAssignment> DeliveryAssignments { get; set; } = new List<DeliveryAssignment>();
}
