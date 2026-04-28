using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("StationAuditLogs", Schema = "sorting")]
public class StationAuditLog
{
    [Key]
    public int Id { get; set; }

    public int StationId { get; set; }

    /// DISABLE | ENABLE | CLEAR | ASSIGN | UNASSIGN
    [Column(TypeName = "nvarchar(20)")]
    public string Action { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(50)")]
    public string OperatorId { get; set; } = string.Empty;

    [Column(TypeName = "nvarchar(255)")]
    public string? Reason { get; set; }

    [Column(TypeName = "nvarchar(50)")]
    public string? PalletId { get; set; }

    public DateTime At { get; set; } = DateTime.UtcNow;
}
