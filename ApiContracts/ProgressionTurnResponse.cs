namespace testapi1.ApiContracts
{
    public sealed class ProgressionTurnResponse
    {
        public string sessionId { get; set; } = "";
        public string replyText { get; set; } = "";
        public string intent { get; set; } = "";
        public float confidence { get; set; }
        public string eventType { get; set; } = "";
        public string evidenceId { get; set; } = "";
        public bool transitioned { get; set; }
        public string transitionReason { get; set; } = "";
        public ProgressionSnapshotResponse snapshot { get; set; } = new();
    }
}
