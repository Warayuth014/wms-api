using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WmsApi.Models;

[Table("UnloadSessions", Schema = "unload")]
public class UnloadSession
{
    [Key]
    public int SessionId { get; set; }
    public string PalletId { get; set; } = string.Empty;
    public string Status { get; set; } = "STEP1";
    public string OperatorId { get; set; } = string.Empty;
    public DateTime? Step1DoneAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(PalletId))]
    public Pallet? Pallet { get; set; }

    [ForeignKey(nameof(OperatorId))]
    public User? Operator { get; set; }

    public ICollection<UnloadLine> UnloadLines { get; set; } = [];
}
