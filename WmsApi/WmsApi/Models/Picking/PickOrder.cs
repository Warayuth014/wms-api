using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("PickOrders", Schema = "picking")]
public class PickOrder
{
    [Key]
    [Column(TypeName = "nvarchar(50)")]
    public string PickOrderId { get; set; } = string.Empty;

    public string Status { get; set; } = "OPEN";

    [Column(TypeName = "nvarchar(50)")]
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(CreatedBy))]
    public User? Creator { get; set; }

    public ICollection<PickOrderDetail> Details { get; set; } = [];
}
