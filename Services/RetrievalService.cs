using Microsoft.EntityFrameworkCore;
using testapi1.Application;
using testapi1.Domain;
using testapi1.Infrastructure.Persistence;
using testapi1.Services.Dialogue;

namespace testapi1.Services
{
    public sealed class RetrievalService : IRetrievalService
    {
        private const int MaxRecentExchanges = 6;
        private const int MaxLoreSnippets = 6;
        private readonly AppDbContext _db;
        private readonly IProgressionSessionStore _sessionStore;

        public RetrievalService(AppDbContext db, IProgressionSessionStore sessionStore)
        {
            _db = db;
            _sessionStore = sessionStore;
        }

        public async Task<NpcDialogueWorldContext?> GetNpcDialogueContextAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            var progression = await _sessionStore.GetAsync(sessionId, cancellationToken);
            if (progression is null)
            {
                return null;
            }

            var npc = await _db.Npcs
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.NpcCode == progression.NpcId, cancellationToken);
            if (npc is null)
            {
                return null;
            }

            var player = await _db.Players
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.PlayerId == progression.PlayerId, cancellationToken);
            if (player is null)
            {
                return null;
            }

            var relationship = await _db.PlayerNpcStates
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.PlayerId == progression.PlayerId && item.NpcId == npc.NpcId,
                    cancellationToken);

            var interactions = await _db.Interactions
                .AsNoTracking()
                .Where(item => item.PlayerId == progression.PlayerId && item.NpcId == npc.NpcId)
                .OrderByDescending(item => item.OccurredAt)
                .Take(MaxRecentExchanges)
                .ToListAsync(cancellationToken);

            var loreChunks = await _db.LoreChunks
                .AsNoTracking()
                .Include(item => item.Doc)
                .Where(item =>
                    item.IsActive &&
                    item.Doc != null &&
                    item.Doc.IsActive &&
                    (item.Doc.NpcId == null || item.Doc.NpcId == npc.NpcId))
                .OrderByDescending(item => item.Doc!.NpcId == npc.NpcId)
                .ThenBy(item => item.DocId)
                .ThenBy(item => item.ChunkOrder)
                .ToListAsync(cancellationToken);

            var loreDocs = loreChunks
                .Where(item => item.Doc is not null)
                .Select(item => item.Doc!)
                .GroupBy(item => item.DocId)
                .Select(group => group.First())
                .ToList();

            var allowedTopics = NpcRevealPolicy.GetAllowedTopics(progression);
            var topicGuidance = NpcRevealPolicy.GetTopicGuidance(allowedTopics);

            return new NpcDialogueWorldContext(
                SessionId: progression.SessionId,
                PlayerId: player.PlayerId,
                PlayerName: string.IsNullOrWhiteSpace(player.DisplayName) ? $"Player {player.PlayerId}" : player.DisplayName,
                NpcDbId: npc.NpcId,
                NpcId: npc.NpcCode,
                NpcName: string.IsNullOrWhiteSpace(npc.Name) ? npc.NpcCode : npc.Name,
                Progression: progression,
                PublicStory: ResolvePublicStory(npc, loreDocs, loreChunks),
                TruthSummary: ResolveTruthSummary(npc, loreDocs, loreChunks),
                Timeline: ResolveTimeline(loreDocs, loreChunks),
                AllowedTopics: allowedTopics,
                TopicGuidance: topicGuidance,
                Relationship: new RelationshipSnapshot(
                    Trust: relationship?.Trust ?? 0.50m,
                    Patience: relationship?.Patience ?? 0.50m,
                    Curiosity: relationship?.Curiosity ?? 0.50m,
                    Openness: relationship?.Openness ?? 0.50m,
                    Memory: relationship?.Memory ?? string.Empty),
                RecentExchanges: interactions
                    .OrderBy(item => item.OccurredAt)
                    .Select(item => new ConversationExchange(
                        OccurredAtUtc: new DateTimeOffset(DateTime.SpecifyKind(item.OccurredAt, DateTimeKind.Utc)),
                        PlayerText: item.PlayerText ?? string.Empty,
                        NpcReply: item.ResponseText ?? string.Empty,
                        IntentCode: item.NluTopIntent ?? string.Empty,
                        ResponseSource: item.ResponseSource ?? string.Empty))
                    .ToList(),
                LoreSnippets: loreChunks
                    .Take(MaxLoreSnippets)
                    .Select(item => new LoreSnippet(
                        Key: item.ChunkKey ?? $"chunk_{item.ChunkId}",
                        Title: item.Doc?.Title ?? string.Empty,
                        Body: item.ChunkText ?? string.Empty))
                    .ToList());
        }

        public async Task PersistNpcReplyAsync(
            NpcReplyPersistenceRecord record,
            CancellationToken cancellationToken = default)
        {
            var occurredAtUtc = record.OccurredAtUtc.UtcDateTime;
            Interaction? interaction;

            if (record.InteractionId.HasValue)
            {
                interaction = await _db.Interactions
                    .FirstOrDefaultAsync(item => item.InteractionId == record.InteractionId.Value, cancellationToken);

                if (interaction is null)
                {
                    throw new InvalidOperationException(
                        $"Interaction '{record.InteractionId.Value}' was not found for session '{record.SessionId}'.");
                }

                if (interaction.PlayerId != record.PlayerId || interaction.NpcId != record.NpcDbId)
                {
                    throw new InvalidOperationException(
                        $"Interaction '{record.InteractionId.Value}' does not belong to player '{record.PlayerId}' and NPC '{record.NpcDbId}'.");
                }
            }
            else
            {
                var recentCandidates = await _db.Interactions
                    .Where(item => item.PlayerId == record.PlayerId && item.NpcId == record.NpcDbId)
                    .OrderByDescending(item => item.OccurredAt)
                    .Take(5)
                    .ToListAsync(cancellationToken);

                interaction = recentCandidates.FirstOrDefault(item =>
                    string.Equals(item.PlayerText ?? string.Empty, record.PlayerText ?? string.Empty, StringComparison.Ordinal) &&
                    Math.Abs((item.OccurredAt - occurredAtUtc).TotalMinutes) <= 10d);
            }

            if (interaction is null)
            {
                interaction = new Interaction
                {
                    PlayerId = record.PlayerId,
                    NpcId = record.NpcDbId,
                    OccurredAt = occurredAtUtc,
                    Location = "interrogation-room",
                    PlayerAction = record.IntentCode,
                    PlayerText = record.PlayerText,
                    NluTopIntent = record.IntentCode,
                    Sentiment = 0.00m,
                    Friendliness = 0.00m,
                    ToneTag = "generated",
                    NsfwFlag = false,
                    ResponseText = record.ResponseText,
                    ResponseSource = record.ResponseSource,
                    ModelVersion = record.ModelVersion,
                    RewardScore = 0.0000m,
                    OutcomeFlags = "npc_reply_generated"
                };

                _db.Interactions.Add(interaction);
            }
            else
            {
                interaction.ResponseText = record.ResponseText;
                interaction.ResponseSource = record.ResponseSource;
                interaction.ModelVersion = record.ModelVersion;
                interaction.NluTopIntent = string.IsNullOrWhiteSpace(interaction.NluTopIntent)
                    ? record.IntentCode
                    : interaction.NluTopIntent;
                interaction.OutcomeFlags = AppendOutcomeFlag(interaction.OutcomeFlags, "npc_reply_generated");
            }

            var state = await _db.PlayerNpcStates.FindAsync(new object[] { record.PlayerId, record.NpcDbId }, cancellationToken);
            if (state is not null)
            {
                state.LastInteractionAt = occurredAtUtc;
                state.Memory = $"intent={record.IntentCode};source={record.ResponseSource};at={record.OccurredAtUtc:O}";
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        private static string ResolvePublicStory(
            Npc npc,
            IReadOnlyCollection<LoreDoc> docs,
            IReadOnlyCollection<LoreChunk> chunks)
        {
            var explicitDoc = docs.FirstOrDefault(item =>
                MatchesKey(item.DocKey, "public_story", "cover_story"));
            if (explicitDoc is not null && !string.IsNullOrWhiteSpace(explicitDoc.Body))
            {
                return explicitDoc.Body;
            }

            var timelineDoc = docs.FirstOrDefault(item => MatchesKey(item.DocKey, "timeline"));
            if (timelineDoc is not null && !string.IsNullOrWhiteSpace(timelineDoc.Body))
            {
                return timelineDoc.Body;
            }

            var timelineChunk = chunks.FirstOrDefault(item => MatchesKey(item.Doc?.DocKey, "timeline"));
            if (timelineChunk is not null && !string.IsNullOrWhiteSpace(timelineChunk.ChunkText))
            {
                return timelineChunk.ChunkText;
            }

            if (string.Equals(npc.NpcCode, "dylan", StringComparison.OrdinalIgnoreCase))
            {
                return "Dylan maintains that he had only limited contact with Elsa, left before anything violent happened, and knows nothing about missing evidence.";
            }

            return $"{npc.Name} insists there is nothing incriminating to explain.";
        }

        private static string ResolveTruthSummary(
            Npc npc,
            IReadOnlyCollection<LoreDoc> docs,
            IReadOnlyCollection<LoreChunk> chunks)
        {
            var explicitDoc = docs.FirstOrDefault(item =>
                MatchesKey(item.DocKey, "truth", "internal_truth", "case_truth"));
            if (explicitDoc is not null && !string.IsNullOrWhiteSpace(explicitDoc.Body))
            {
                return explicitDoc.Body;
            }

            var explicitChunk = chunks.FirstOrDefault(item =>
                MatchesKey(item.Doc?.DocKey, "truth", "internal_truth", "case_truth"));
            if (explicitChunk is not null && !string.IsNullOrWhiteSpace(explicitChunk.ChunkText))
            {
                return explicitChunk.ChunkText;
            }

            if (string.Equals(npc.NpcCode, "dylan", StringComparison.OrdinalIgnoreCase))
            {
                return "Dylan knows he is responsible for Elsa's death, knows the financial motive trail is real, and knows the timeline and evidence cleanup story he presents is false.";
            }

            return $"{npc.Name} knows the full truth of the case and is actively withholding it.";
        }

        private static IReadOnlyList<string> ResolveTimeline(
            IReadOnlyCollection<LoreDoc> docs,
            IReadOnlyCollection<LoreChunk> chunks)
        {
            var timeline = chunks
                .Where(item => MatchesKey(item.Doc?.DocKey, "timeline") || MatchesText(item.Doc?.Title, "timeline"))
                .OrderBy(item => item.ChunkOrder)
                .Select(item => item.ChunkText)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Take(4)
                .ToList();

            if (timeline.Count > 0)
            {
                return timeline;
            }

            var fallback = docs
                .Where(item => MatchesKey(item.DocKey, "timeline", "briefing"))
                .Select(item => item.Body)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Take(3)
                .ToList();

            if (fallback.Count > 0)
            {
                return fallback;
            }

            return Array.Empty<string>();
        }

        private static bool MatchesKey(string? value, params string[] tokens)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
        }

        private static bool MatchesText(string? value, string token)
        {
            return !string.IsNullOrWhiteSpace(value) &&
                   value.Contains(token, StringComparison.OrdinalIgnoreCase);
        }

        private static string AppendOutcomeFlag(string? current, string flag)
        {
            if (string.IsNullOrWhiteSpace(current))
            {
                return flag;
            }

            return current.Contains(flag, StringComparison.OrdinalIgnoreCase)
                ? current
                : $"{current};{flag}";
        }
    }
}
