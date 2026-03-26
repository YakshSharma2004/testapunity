namespace testapi1.ApiContracts
{
    public sealed class StartProgressionRequest
    {
        public int? playerId { get; set; }
        public string caseId { get; set; } = "dylan-interrogation";
        public string npcId { get; set; } = "dylan";
        public string contextKey { get; set; } = "";
        public string? sessionId { get; set; }
    }
}
