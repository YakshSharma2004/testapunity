namespace testapi1.Contracts
{
    public class LlmRawResponse
    {
        public string responseText { get; set; } = "";
        public string modelName { get; set; } = "";
        public int tokensUsed { get; set; }
        public string finishReason { get; set; } = "";
    }
}
