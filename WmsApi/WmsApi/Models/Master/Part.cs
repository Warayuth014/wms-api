using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("Parts", Schema = "master")]
public class Part
{
    [Key]
    public string PartId { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string ItemDesc { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int? MinStock { get; set; }
    public int? MaxStock { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
