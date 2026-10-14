using Bisuteria.Domain.Common;
namespace Bisuteria.Domain.Entities;

public sealed class EmailOutbox : BaseEntity
{
    public string Recipient { get; set; } = "";
    public string Kind { get; set; } = "Notification";
    public string Subject { get; set; } = "";
    public string TextBody { get; set; } = "";
    public int Attempts { get; set; }
    public DateTime NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public string? LastError { get; set; }
}
