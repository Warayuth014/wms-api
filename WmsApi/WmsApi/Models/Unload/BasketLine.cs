using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("BasketLines", Schema = "unload")]
public class BasketLine
{
    [Key]
    public int LineId { get; set; }

    public int SessionId { get; set; }
    public int UnloadLineId { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string BasketId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string PartId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string PalletId { get; set; } = string.Empty;

    public string? LotNumber { get; set; }
    public DateOnly? ExpiredDate { get; set; }

    public int QtyLoaded { get; set; }
    public string Status { get; set; } = "LOADED"; // LOADED | CANCELLED

    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "nvarchar(50)")]
    public string OperatorId { get; set; } = string.Empty;

    [ForeignKey(nameof(SessionId))]
    public UnloadSession? Session { get; set; }

    [ForeignKey(nameof(UnloadLineId))]
    public UnloadLine? UnloadLine { get; set; }

    [ForeignKey(nameof(BasketId))]
    public Basket? Basket { get; set; }

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }
}
