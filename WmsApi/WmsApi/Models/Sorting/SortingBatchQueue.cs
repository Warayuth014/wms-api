using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

/// <summary>
/// Batch ที่ถูกสร้างจาก test_sorting แต่ตอนนั้น 10 station busy หมด
/// → รอใน queue, simulator จะ assign เมื่อมี station ว่าง
/// </summary>
[Table("SortingBatchQueues", Schema = "sorting")]
public class SortingBatchQueue
{
    [Key]
    public int Id { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AssignedAt { get; set; }

    /// JSON array ของ packing ids ที่จะรวมเป็น batch
    [Column(TypeName = "nvarchar(max)")]
    public string PackingIdsJson { get; set; } = "[]";

    [Column(TypeName = "nvarchar(20)")]
    public string Status { get; set; } = "WAITING";   // WAITING | ASSIGNED | CANCELLED

    [Column(TypeName = "nvarchar(50)")]
    public string? AssignedPalletId { get; set; }

    [ForeignKey(nameof(AssignedPalletId))]
    public SortingPallet? AssignedPallet { get; set; }
}
