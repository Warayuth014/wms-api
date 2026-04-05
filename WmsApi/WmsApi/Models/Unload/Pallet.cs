using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("Pallets", Schema = "unload")]
public class Pallet
{
    [Key]
    public string PalletId { get; set; } = string.Empty;
    public string? Type { get; set; }
    public string Status { get; set; } = "AVAILABLE";
    public string? Location { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ReceiptLine> ReceiptLines { get; set; } = [];
    public ICollection<UnloadLine> UnloadLines { get; set; } = [];
}
