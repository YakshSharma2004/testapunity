namespace testapi1.ApiContracts
{
    public sealed class ProgressionClueClickResponse
    {
        public string sessionId { get; set; } = "";
        public string clueId { get; set; } = "";
        public string unlockTopic { get; set; } = "";
        public bool isFirstDiscovery { get; set; }
        public bool applied { get; set; }
        public string reason { get; set; } = "";
        public ProgressionSnapshotResponse snapshot { get; set; } = new();
    }
}
