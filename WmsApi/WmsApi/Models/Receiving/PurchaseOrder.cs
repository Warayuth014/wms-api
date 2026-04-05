using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("PurchaseOrders", Schema = "receiving")]
public class PurchaseOrder
{
    [Key]
    public string POId { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(SupplierId))]
    public Supplier? Supplier { get; set; }

    [ForeignKey(nameof(CreatedBy))]
    public User? Creator { get; set; }

    public ICollection<POItem> Items { get; set; } = [];
}
