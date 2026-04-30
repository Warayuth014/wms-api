using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("CancelLog", Schema = "audit")]
public class CancelLog
{
    [Key]
    public int CancelId { get; set; }
    public string RefType { get; set; } = string.Empty;
    public int RefId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string RequestBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public string Status { get; set; } = "PENDING";
    public DateTime? CancelledAt { get; set; }

    [ForeignKey(nameof(RequestBy))]
    public User? Requester { get; set; }

    [ForeignKey(nameof(ApprovedBy))]
    public User? Approver { get; set; }
}
