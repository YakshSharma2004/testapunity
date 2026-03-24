using testapi1.Domain.Progression;

namespace testapi1.Application
{
    public interface IRetrievalService
    {
        Task<NpcDialogueWorldContext?> GetNpcDialogueContextAsync(
            string sessionId,
            CancellationToken cancellationToken = default);

        Task PersistNpcReplyAsync(
            NpcReplyPersistenceRecord record,
            CancellationToken cancellationToken = default);
    }

    public sealed record RelationshipSnapshot(
        decimal Trust,
        decimal Patience,
        decimal Curiosity,
        decimal Openness,
        string Memory);

    public sealed record ConversationExchange(
        DateTimeOffset OccurredAtUtc,
        string PlayerText,
        string NpcReply,
        string IntentCode,
        string ResponseSource);

    public sealed record LoreSnippet(
        string Key,
        string Title,
        string Body);

    public sealed record NpcDialogueWorldContext(
        string SessionId,
        int PlayerId,
        string PlayerName,
        int NpcDbId,
        string NpcId,
        string NpcName,
        ProgressionSessionState Progression,
        string PublicStory,
        string TruthSummary,
        IReadOnlyList<string> Timeline,
        IReadOnlyList<string> AllowedTopics,
        IReadOnlyDictionary<string, string> TopicGuidance,
        RelationshipSnapshot Relationship,
        IReadOnlyList<ConversationExchange> RecentExchanges,
        IReadOnlyList<LoreSnippet> LoreSnippets);

    public sealed record NpcReplyPersistenceRecord(
        string SessionId,
        int PlayerId,
        int NpcDbId,
        string PlayerText,
        string IntentCode,
        string ResponseText,
        string ResponseSource,
        string ModelVersion,
        DateTimeOffset OccurredAtUtc);
}
