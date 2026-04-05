using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("PreworkCutLogs", Schema = "putaway")]
public class PreworkCutLog
{
    [Key]
    public int Id { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string PalletId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(20)")]
    public string StationId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string PartId { get; set; } = string.Empty;

    public string? Owner { get; set; }
    public string? Brand { get; set; }
    public string? ItemDesc { get; set; }
    public string? ImageUrl { get; set; }
    public int Qty { get; set; }
    public string? LotNumber { get; set; }
    public DateOnly? ExpiredDate { get; set; }
    public string Condition { get; set; } = "PW";

    [Column(TypeName = "nvarchar(20)")]
    public string OperatorId { get; set; } = "SYSTEM";

    public DateTime CutAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }
}
