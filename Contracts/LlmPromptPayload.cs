namespace testapi1.Contracts
{
    public class LlmPromptPayload
    {
        public string promptText { get; set; } = "";
        public string conversationId { get; set; } = "";
        public string systemContext { get; set; } = "";
        public string npcId { get; set; } = "";
        public string contextKey { get; set; } = "";
        public int maxTokens { get; set; } = 256;
    }
}
