using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("SortSessions", Schema = "sorting")]
public class SortSession
{
    [Key]
    public int SessionId { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string StationId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string SortPalletId { get; set; } = string.Empty;

    public string Status { get; set; } = "OPEN"; // OPEN | CLOSED

    public string OperatorId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    [ForeignKey(nameof(StationId))]
    public SortStation? Station { get; set; }

    [ForeignKey(nameof(SortPalletId))]
    public Pallet? SortPallet { get; set; }

    public ICollection<SortSessionItem> Items { get; set; } = [];
}
