namespace testapi1.ApiContracts
{
    public sealed class ProgressionSnapshotResponse
    {
        public string sessionId { get; set; } = "";
        public string caseId { get; set; } = "";
        public string npcId { get; set; } = "";
        public string state { get; set; } = "";
        public int turnCount { get; set; }
        public int trustScore { get; set; }
        public int shutdownScore { get; set; }
        public bool isTerminal { get; set; }
        public string ending { get; set; } = "";
        public List<string> allowedIntents { get; set; } = new();
        public List<string> evidencePresented { get; set; } = new();
        public List<string> discoveredClueIds { get; set; } = new();
        public List<string> discussedClueIds { get; set; } = new();
        public bool canConfess { get; set; }
        public string proofTier { get; set; } = "";
        public string composureState { get; set; } = "";
        public string lastTransitionReason { get; set; } = "";
    }
}
