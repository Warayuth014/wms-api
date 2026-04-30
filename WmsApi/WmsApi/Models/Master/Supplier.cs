using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("Suppliers", Schema = "master")]
public class Supplier
{
    [Key]
    public string SupplierId { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = [];
}
