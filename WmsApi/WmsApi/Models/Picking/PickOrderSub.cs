using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("PickOrderSubs", Schema = "picking")]
public class PickOrderSub
{
    [Key]
    public int Id { get; set; }
    public int PickOrderDetailId { get; set; }
    public int ReceiptLineId { get; set; }
    public int AllocatedQty { get; set; }
    public int PickedQty { get; set; } = 0;
    public string Status { get; set; } = "PENDING";

    [ForeignKey(nameof(PickOrderDetailId))]
    public PickOrderDetail? PickOrderDetail { get; set; }

    [ForeignKey(nameof(ReceiptLineId))]
    public ReceiptLine? ReceiptLine { get; set; }
}
