using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("PickStations", Schema = "picking")]
public class PickStation
{
    [Key]
    [Column(TypeName = "nvarchar(50)")]
    public string StationId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string? CurrentPalletId { get; set; }

    [ForeignKey(nameof(CurrentPalletId))]
    public Pallet? CurrentPallet { get; set; }
}
