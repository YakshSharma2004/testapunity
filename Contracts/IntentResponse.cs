namespace testapi1.Contracts
{
    public class IntentResponse
    {
        public string intent { get; set; } = "";
        public float confidence { get; set; }
        public string notes { get; set; } = "";
    }
}
