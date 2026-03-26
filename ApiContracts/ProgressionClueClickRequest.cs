namespace testapi1.ApiContracts
{
    public sealed class ProgressionClueClickRequest
    {
        public string sessionId { get; set; } = "";
        public string clueId { get; set; } = "";
        public string clueName { get; set; } = "";
        public string source { get; set; } = "";
    }
}
