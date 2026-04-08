using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("PackingDetails", Schema = "packing")]
public class PackingDetail
{
    [Key]
    public int Id { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string PackingId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string PickOrderId { get; set; } = string.Empty;

    public string Status { get; set; } = "PENDING"; // PENDING | DONE

    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(PackingId))]
    public Packing? Packing { get; set; }

    [ForeignKey(nameof(PickOrderId))]
    public PickOrder? PickOrder { get; set; }
}
