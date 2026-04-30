using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("CustomerOrders", Schema = "customer")]
public class CustomerOrder
{
    [Key]
    [Column(TypeName = "nvarchar(50)")]
    public string CustomerOrderId { get; set; } = string.Empty; // CO-{yyyyMMddHHmmss}-{rand}

    [Column(TypeName = "nvarchar(100)")]
    public string Owner { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(20)")]
    public string Status { get; set; } = "ACTIVE"; // ACTIVE | SHIPPED

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime? ShippedAt { get; set; }

    public ICollection<PickOrder> PickOrders { get; set; } = [];
}
