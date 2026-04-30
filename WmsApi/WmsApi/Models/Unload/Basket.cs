using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("Baskets", Schema = "unload")]
public class Basket
{
    [Key]
    [Column(TypeName = "nvarchar(50)")]
    public string BasketId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(100)")]
    public string Label { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string? Zone { get; set; }

    [Column(TypeName = "nvarchar(100)")]
    public string? Destination { get; set; }

    [Column(TypeName = "nvarchar(30)")]
    public string Status { get; set; } = "OPEN"; // OPEN | CLOSED

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<BasketLine> Lines { get; set; } = [];
}
