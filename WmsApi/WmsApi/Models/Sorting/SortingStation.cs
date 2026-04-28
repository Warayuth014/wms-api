using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("SortingStations", Schema = "sorting")]
public class SortingStation
{
    [Key]
    public int StationId { get; set; }   // 1..10 (fixed seed)

    public bool Enabled { get; set; } = true;

    [Column(TypeName = "nvarchar(50)")]
    public string? CurrentPalletId { get; set; }   // FK SortingPallet

    [Column(TypeName = "nvarchar(50)")]
    public string? DisabledBy { get; set; }
    public DateTime? DisabledAt { get; set; }

    [Column(TypeName = "nvarchar(255)")]
    public string? DisableReason { get; set; }

    [ForeignKey(nameof(CurrentPalletId))]
    public SortingPallet? CurrentPallet { get; set; }
}
