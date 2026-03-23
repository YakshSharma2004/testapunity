namespace testapi1.ApiContracts
{
    public sealed class StartProgressionResponse
    {
        public string sessionId { get; set; } = "";
        public ProgressionSnapshotResponse snapshot { get; set; } = new();
    }
}
