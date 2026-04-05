using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("PickOrderDetails", Schema = "picking")]
public class PickOrderDetail
{
    [Key]
    public int Id { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string PickOrderId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string PartId { get; set; } = string.Empty;

    public int RequiredQty { get; set; }
    public int ReservedQty { get; set; } = 0;
    public string Status { get; set; } = "PENDING";

    [ForeignKey(nameof(PickOrderId))]
    public PickOrder? PickOrder { get; set; }

    [ForeignKey(nameof(PartId))]
    public Part? Part { get; set; }

    public ICollection<PickOrderSub> Subs { get; set; } = [];
}
