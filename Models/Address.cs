using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace namHub_FastFood.Models;

public partial class Address
{
    [Key]
    [Column("address_id")]
    public int AddressId { get; set; }

    [Column("customer_id")]
    public int? CustomerId { get; set; }

    [Column("address_line1")]
    [StringLength(255)]
    public string AddressLine1 { get; set; } = null!;

    [Column("address_line2")]
    [StringLength(255)]
    public string? AddressLine2 { get; set; }

    [Column("city")]
    [StringLength(100)]
    public string City { get; set; } = null!;

    [Column("state")]
    [StringLength(100)]
    public string? State { get; set; }

    [Column("postal_code")]
    [StringLength(20)]
    public string? PostalCode { get; set; }

    [Column("country")]
    [StringLength(100)]
    public string Country { get; set; } = null!;

    [Column("is_default")]
    public bool? IsDefault { get; set; }

    [Column("created_at", TypeName = "datetime")]
    public DateTime? CreatedAt { get; set; }

    [Column("updated_at", TypeName = "datetime")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("CustomerId")]
    [InverseProperty("Addresses")]
    public virtual Customer? Customer { get; set; }
}
