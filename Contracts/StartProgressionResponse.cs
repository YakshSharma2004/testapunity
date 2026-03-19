namespace testapi1.Contracts
{
    public sealed class StartProgressionResponse
    {
        public string sessionId { get; set; } = "";
        public ProgressionSnapshotResponse snapshot { get; set; } = new();
    }
}
