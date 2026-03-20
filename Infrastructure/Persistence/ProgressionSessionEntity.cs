
namespace testapi1.Infrastructure.Persistence
{
    public class ProgressionSessionEntity
    {
        public string SessionId { get; set; } = default!;       // PK
        public string CaseId { get; set; } = default!;
        public string NpcId { get; set; } = default!;
        public string State { get; set; } = default!;           
        public int TurnCount { get; set; }
        public int TrustScore { get; set; }
        public int ShutdownScore { get; set; }
        public bool IsTerminal { get; set; }
        public string Ending { get; set; } = default!;         
        public string PresentedEvidenceJson { get; set; } = "[]"; 
        public string HistoryJson { get; set; } = "[]";         
        public string LastTransitionReason { get; set; } = default!;
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset UpdatedAtUtc { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
    }
}
