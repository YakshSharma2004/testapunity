namespace testapi1.ApiContracts
{
    public sealed class NpcDialogueResponse
    {
        public string sessionId { get; set; } = string.Empty;
        public string replyText { get; set; } = string.Empty;
        public string modelName { get; set; } = string.Empty;
        public string provider { get; set; } = string.Empty;
        public bool usedFallback { get; set; }
        public string finishReason { get; set; } = string.Empty;
        public string stateUsed { get; set; } = string.Empty;
        public List<string> allowedTopicsUsed { get; set; } = new();
    }
}
