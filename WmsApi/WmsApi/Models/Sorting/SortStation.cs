using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("SortStations", Schema = "sorting")]
public class SortStation
{
    [Key]
    [Column(TypeName = "nvarchar(50)")]
    public string StationId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Status { get; set; } = "AVAILABLE"; // AVAILABLE | BUSY
}
