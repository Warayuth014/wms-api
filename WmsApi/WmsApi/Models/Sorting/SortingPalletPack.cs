using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

/// <summary>
/// Queue ของ Pack ที่กำลังจะ "ไหลเข้า" SortingPallet ทีละชิ้น
/// SortingFlowSimulator background service จะหยิบ row ที่ Status=PENDING และ ScheduledAt &lt;= now
/// แล้วตั้ง Pack.Status = SORTED + บันทึก ProcessedAt
/// </summary>
[Table("SortingPalletPacks", Schema = "sorting")]
public class SortingPalletPack
{
    [Key]
    public int Id { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string PalletId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string PackingId { get; set; } = string.Empty;

    public int SequenceNo { get; set; }   // 1-based ลำดับเข้า

    public DateTime ScheduledAt { get; set; }   // = batch.CreatedAt + (seq * 2s)
    public DateTime? ProcessedAt { get; set; }

    [Column(TypeName = "nvarchar(20)")]
    public string Status { get; set; } = "PENDING";  // PENDING | PROCESSED | CANCELLED

    [ForeignKey(nameof(PalletId))]
    public SortingPallet? Pallet { get; set; }

    [ForeignKey(nameof(PackingId))]
    public Packing? Packing { get; set; }
}
