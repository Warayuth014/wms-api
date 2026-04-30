using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("CheckInEntries", Schema = "packing")]
public class CheckInEntry
{
    [Key]
    public int Id { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string SlotId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string PackingId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(100)")]
    public string Owner { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(20)")]
    public string Status { get; set; } = "CHECKED_IN"; // CHECKED_IN | SHIPPED

    [Column(TypeName = "nvarchar(50)")]
    public string ScannedBy { get; set; } = string.Empty;

    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ShippedAt { get; set; }

    [ForeignKey(nameof(SlotId))]
    public CheckInSlot? Slot { get; set; }

    [ForeignKey(nameof(PackingId))]
    public Packing? Packing { get; set; }
}
