using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("UnloadLines", Schema = "unload")]
public class UnloadLine
{
    [Key]
    public int LineId { get; set; }
    public int SessionId { get; set; }
    public string PalletId { get; set; } = string.Empty;
    public string PartId { get; set; } = string.Empty;
    public string? LotNumber { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public int QtyUnloaded { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTime? ConfirmedAt { get; set; }
    public string OperatorId { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(SessionId))]
    public UnloadSession? Session { get; set; }

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }
}
