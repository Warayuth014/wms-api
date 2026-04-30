using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("PackingPartScans", Schema = "packing")]
public class PackingPartScan
{
    [Key]
    public int Id { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string PackingId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string PickOrderId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string PartId { get; set; } = string.Empty;

    public int ScannedQty { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string ScannedBy { get; set; } = string.Empty;

    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(PackingId))]
    public Packing? Packing { get; set; }
}
