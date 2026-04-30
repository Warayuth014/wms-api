using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("CheckInSlots", Schema = "packing")]
public class CheckInSlot
{
    [Key]
    [Column(TypeName = "nvarchar(50)")]
    public string SlotId { get; set; } = string.Empty; // เช่น SLOT-01

    [Column(TypeName = "nvarchar(100)")]
    public string Owner { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string? CustomerOrderId { get; set; }

    [Column(TypeName = "nvarchar(20)")]
    public string Status { get; set; } = "OPEN"; // OPEN | READY | SHIPPED

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? ShippedAt { get; set; }

    [ForeignKey(nameof(CustomerOrderId))]
    public CustomerOrder? CustomerOrder { get; set; }

    public ICollection<CheckInEntry> Entries { get; set; } = [];
}
