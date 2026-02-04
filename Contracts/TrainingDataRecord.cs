using System;

namespace testapi1.Contracts
{
    public class TrainingDataRecord
    {
        public string id { get; set; } = "";
        public DateTimeOffset receivedAt { get; set; } = DateTimeOffset.UtcNow;
        public string userText { get; set; } = "";
        public string predictedIntent { get; set; } = "";
        public float confidence { get; set; }
        public string modelVersion { get; set; } = "";
        public string? correctedIntent { get; set; }
        public string? correctionNotes { get; set; }
        public string? npcId { get; set; }
        public string? contextKey { get; set; }
    }
}
