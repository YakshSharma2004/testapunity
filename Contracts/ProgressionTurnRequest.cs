namespace testapi1.Contracts
{
    public sealed class ProgressionTurnRequest
    {
        public string sessionId { get; set; } = "";
        public string text { get; set; } = "";
        public string npcId { get; set; } = "";
        public string contextKey { get; set; } = "";
    }
}
