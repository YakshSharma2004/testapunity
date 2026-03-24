namespace testapi1.Services.Llm
{
    public sealed class LlmOptions
    {
        public LocalLlmOptions Local { get; set; } = new();
        public RemoteLlmOptions Remote { get; set; } = new();
        public LlmGenerationOptions Generation { get; set; } = new();
    }

    public sealed class LocalLlmOptions
    {
        public bool Enabled { get; set; } = true;
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string Model { get; set; } = string.Empty;
        public int TimeoutMs { get; set; } = 60000;
    }

    public sealed class RemoteLlmOptions
    {
        public bool Enabled { get; set; }
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
        public string Model { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public int TimeoutMs { get; set; } = 60000;
    }

    public sealed class LlmGenerationOptions
    {
        public int MaxTokens { get; set; } = 256;
        public double Temperature { get; set; } = 0.35d;
    }
}
