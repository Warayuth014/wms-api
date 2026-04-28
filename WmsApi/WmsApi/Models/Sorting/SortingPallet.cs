using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("SortingPallets", Schema = "sorting")]
public class SortingPallet
{
    [Key]
    [Column(TypeName = "nvarchar(50)")]
    public string PalletId { get; set; } = string.Empty;  // SP-01, SP-02

    // OPEN = รับ carton อยู่
    // FULL = ครบ capacity แล้ว (auto-seal)
    // SEALED = operator กด seal เอง
    // DISPATCHED = ย้ายไป docking area แล้ว
    [Column(TypeName = "nvarchar(20)")]
    public string Status { get; set; } = "OPEN";

    public int CartonsCount { get; set; }

    /// MaxCapacity ถูกกำหนดตอน batch ถูกสร้าง (ตามจำนวน packing ที่เลือก)
    public int MaxCapacity { get; set; } = 12;

    /// Station ที่ pallet นี้อยู่ (1..10) — null = ยังไม่ได้ assign
    public int? StationId { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SealedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }

    public ICollection<Packing> Packings { get; set; } = [];
}
