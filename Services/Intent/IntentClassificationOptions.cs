namespace testapi1.Services.Intent
{
    public sealed class IntentClassificationOptions
    {
        public int TopK { get; set; } = 3;
        public double MinConfidence { get; set; } = 0.5;
        public bool IncludeDebugNotes { get; set; } = true;
    }
}
