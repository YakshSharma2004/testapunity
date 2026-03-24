namespace testapi1.ApiContracts
{
    public class LlmRawResponse
    {
        public string responseText { get; set; } = "";
        public string modelName { get; set; } = "";
        public int tokensUsed { get; set; }
        public string finishReason { get; set; } = "";
        public string provider { get; set; } = "";
        public bool usedFallback { get; set; }
    }
}
