using testapi1.ApiContracts;

namespace testapi1.Application
{
    public sealed record ResolvedPlayerTurn(
        string SessionId,
        string Text,
        string NpcId,
        string ContextKey,
        IReadOnlyList<string> DiscussedClueIds,
        string NormalizedText,
        IntentResponse Intent);
}
