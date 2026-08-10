using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("POItemLots", Schema = "receiving")]
public class POItemLot
{
    [Key]
    public int Id { get; set; }
    public int POItemId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public int QtyOrdered { get; set; }

    [ForeignKey(nameof(POItemId))]
    public POItem? POItem { get; set; }
}
