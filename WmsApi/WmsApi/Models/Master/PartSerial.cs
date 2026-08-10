using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("PartSerials", Schema = "master")]
public class PartSerial
{
    [Key]
    public int Id { get; set; }

    public string PartId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(80)")]
    public string SerialNo { get; set; } = string.Empty;

    // Lot ที่ serial นี้มาจาก — ของจริงจะมาพร้อม serial ตั้งแต่แรก
    // ผูกกับ POItemLot โดยตรง (ละเอียดสุด) เพราะจาก 1 FK นี้รู้ได้ครบทั้ง PO/Condition/Lot
    // ผ่าน POItemLot.POItem — ไม่ต้องผูกกับ POItem แยกต่างหาก
    public int? POItemLotId { get; set; }

    public int? ReceiptLineId { get; set; }

    public string? PalletId { get; set; }

    [Column(TypeName = "nvarchar(20)")]
    public string Status { get; set; } = "STORED";

    [Column(TypeName = "nvarchar(50)")]
    public string? PackingId { get; set; }

    public DateTime? PackedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }

    [ForeignKey(nameof(POItemLotId))]
    public POItemLot? POItemLot { get; set; }

    [ForeignKey(nameof(ReceiptLineId))]
    public ReceiptLine? ReceiptLine { get; set; }

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    [ForeignKey(nameof(PackingId))]
    public Packing? Packing { get; set; }
}
