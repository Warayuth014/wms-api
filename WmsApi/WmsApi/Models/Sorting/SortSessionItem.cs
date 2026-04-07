using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("SortSessionItems", Schema = "sorting")]
public class SortSessionItem
{
    [Key]
    public int Id { get; set; }

    public int SessionId { get; set; }

    /// <summary>
    /// Pallet ที่ถูก sort เข้า session (มาจาก Packing — status=SHIPPED)
    /// ในที่นี้ 1 carton = 1 source pallet
    /// </summary>
    [Column(TypeName = "nvarchar(50)")]
    public string SourcePalletId { get; set; } = string.Empty;

    public string? TrackingId { get; set; }

    public DateTime ScannedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(SessionId))]
    public SortSession? Session { get; set; }

    [ForeignKey(nameof(SourcePalletId))]
    public Pallet? SourcePallet { get; set; }
}
