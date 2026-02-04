namespace testapi1.Services
{
    public class ApiCacheOptions
    {
        public int IntentTtlSeconds { get; set; } = 120;
        public int LlmTtlSeconds { get; set; } = 60;
        public string ModelVersion { get; set; } = "v1";
    }
}
