namespace testapi1.ApiContracts
{
    public class LlmPromptPayload
    {
        public string promptText { get; set; } = "";
        public string conversationId { get; set; } = "";
        public string systemContext { get; set; } = "";
        public string npcId { get; set; } = "";
        public string contextKey { get; set; } = "";
        public int maxTokens { get; set; } = 256;
        public bool requireJson { get; set; }
        public double? temperature { get; set; }
    }
}
