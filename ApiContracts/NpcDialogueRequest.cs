namespace testapi1.ApiContracts
{
    public sealed class NpcDialogueRequest
    {
        public string sessionId { get; set; } = string.Empty;
        public string text { get; set; } = string.Empty;
        public string contextKey { get; set; } = string.Empty;
        public int maxTokens { get; set; }
    }
}
