namespace testapi1.ApiContracts
{
    public class IntentResponse
    {
        public string intent { get; set; } = "";
        public float confidence { get; set; }
        public string notes { get; set; } = "";
        public string modelVersion { get; set; } = "";
    }
}
