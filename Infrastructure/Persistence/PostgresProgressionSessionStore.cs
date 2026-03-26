
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using testapi1.Application;
using testapi1.Domain.Progression;
using testapi1.Services.Progression;

namespace testapi1.Infrastructure.Persistence
{
    public sealed class PostgresProgressionSessionStore : IProgressionSessionStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly AppDbContext _db;
        private readonly IOptionsMonitor<ProgressionOptions> _options;

        public PostgresProgressionSessionStore(
            AppDbContext db,
            IOptionsMonitor<ProgressionOptions> options)
        {
            _db = db;
            _options = options;
        }

        public async Task<ProgressionSessionState?> GetAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sessionId)) return null;

            var entity = await _db.ProgressionSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);

            if (entity is null) return null;

            // Treat expired sessions as non-existent
            if (entity.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            {
                _db.ProgressionSessions.Remove(entity);
                await _db.SaveChangesAsync(cancellationToken);
                return null;
            }

            return MapToState(entity);
        }

        public async Task SetAsync(
            ProgressionSessionState state,
            CancellationToken cancellationToken = default)
        {
            var ttlMinutes = Math.Max(1, _options.CurrentValue.SessionTtlMinutes);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(ttlMinutes);

            var existing = await _db.ProgressionSessions
                .FirstOrDefaultAsync(s => s.SessionId == state.SessionId, cancellationToken);

            if (existing is null)
            {
                _db.ProgressionSessions.Add(MapToEntity(state, expiresAt));
            }
            else
            {
                existing.PlayerId = state.PlayerId;
                existing.State = state.State.ToString();
                existing.TurnCount = state.TurnCount;
                existing.TrustScore = state.TrustScore;
                existing.ShutdownScore = state.ShutdownScore;
                existing.IsTerminal = state.IsTerminal;
                existing.Ending = state.Ending.ToString();
                existing.PresentedEvidenceJson = JsonSerializer.Serialize(
                    state.PresentedEvidence.Select(e => e.ToString()), JsonOptions);
                existing.DiscoveredClueIdsJson = JsonSerializer.Serialize(
                    state.DiscoveredClues.Select(ClueCatalog.ToKey), JsonOptions);
                existing.DiscussedClueIdsJson = JsonSerializer.Serialize(
                    state.DiscussedClues.Select(ClueCatalog.ToKey), JsonOptions);
                existing.ClueClickHistoryJson = JsonSerializer.Serialize(
                    state.ClueClickHistory.Select(MapToStoredClueClick),
                    JsonOptions);
                existing.ComposureState = state.ComposureState.ToString();
                existing.ProofTier = state.ProofTier.ToString();
                existing.CanConfess = state.CanConfess;
                existing.HistoryJson = JsonSerializer.Serialize(state.History, JsonOptions);
                existing.LastTransitionReason = state.LastTransitionReason;
                existing.UpdatedAtUtc = state.UpdatedAtUtc;
                existing.ExpiresAtUtc = expiresAt;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> RemoveAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            var entity = await _db.ProgressionSessions
                .FirstOrDefaultAsync(s => s.SessionId == sessionId, cancellationToken);

            if (entity is null) return false;

            _db.ProgressionSessions.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return true;
        }

        private static ProgressionSessionState MapToState(ProgressionSessionEntity e)
        {
            var evidence = DeserializeList<string>(e.PresentedEvidenceJson);
            var discovered = DeserializeList<string>(e.DiscoveredClueIdsJson);
            var discussed = DeserializeList<string>(e.DiscussedClueIdsJson);
            var clueClicks = DeserializeList<StoredClueClickHistoryEntry>(e.ClueClickHistoryJson);
            var history = DeserializeList<ProgressionHistoryEntry>(e.HistoryJson);

            var discoveredClues = discovered
                .Select(item => ClueCatalog.TryParseKey(item, out var clueId) ? clueId : (ClueId?)null)
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .Distinct()
                .ToList();

            var discussedClues = discussed
                .Select(item => ClueCatalog.TryParseKey(item, out var clueId) ? clueId : (ClueId?)null)
                .Where(item => item.HasValue)
                .Select(item => item!.Value)
                .Distinct()
                .ToList();

            var mappedClicks = clueClicks
                .Select(MapToDomainClueClick)
                .Where(item => item is not null)
                .Select(item => item!)
                .ToList();

            var composureState = Enum.TryParse<ComposureState>(e.ComposureState, out var parsedComposure)
                ? parsedComposure
                : ComposureState.Calm;

            var proofTier = Enum.TryParse<ProofTier>(e.ProofTier, out var parsedProofTier)
                ? parsedProofTier
                : ProofTier.None;

            return new ProgressionSessionState(
                SessionId: e.SessionId,
                PlayerId: e.PlayerId,
                CaseId: e.CaseId,
                NpcId: e.NpcId,
                State: Enum.Parse<ProgressionStateId>(e.State),
                TurnCount: e.TurnCount,
                TrustScore: e.TrustScore,
                ShutdownScore: e.ShutdownScore,
                IsTerminal: e.IsTerminal,
                Ending: Enum.Parse<CaseEndingType>(e.Ending),
                PresentedEvidence: evidence.Select(Enum.Parse<EvidenceId>).ToList(),
                DiscoveredClues: discoveredClues,
                DiscussedClues: discussedClues,
                ClueClickHistory: mappedClicks,
                ComposureState: composureState,
                ProofTier: proofTier,
                CanConfess: e.CanConfess,
                History: history,
                LastTransitionReason: e.LastTransitionReason,
                CreatedAtUtc: e.CreatedAtUtc,
                UpdatedAtUtc: e.UpdatedAtUtc
            );
        }

        private static ProgressionSessionEntity MapToEntity(
            ProgressionSessionState s, DateTimeOffset expiresAt) => new()
            {
                SessionId = s.SessionId,
                PlayerId = s.PlayerId,
                CaseId = s.CaseId,
                NpcId = s.NpcId,
                State = s.State.ToString(),
                TurnCount = s.TurnCount,
                TrustScore = s.TrustScore,
                ShutdownScore = s.ShutdownScore,
                IsTerminal = s.IsTerminal,
                Ending = s.Ending.ToString(),
                PresentedEvidenceJson = JsonSerializer.Serialize(
                s.PresentedEvidence.Select(e => e.ToString()), JsonOptions),
                DiscoveredClueIdsJson = JsonSerializer.Serialize(
                s.DiscoveredClues.Select(ClueCatalog.ToKey), JsonOptions),
                DiscussedClueIdsJson = JsonSerializer.Serialize(
                s.DiscussedClues.Select(ClueCatalog.ToKey), JsonOptions),
                ClueClickHistoryJson = JsonSerializer.Serialize(
                s.ClueClickHistory.Select(MapToStoredClueClick), JsonOptions),
                ComposureState = s.ComposureState.ToString(),
                ProofTier = s.ProofTier.ToString(),
                CanConfess = s.CanConfess,
                HistoryJson = JsonSerializer.Serialize(s.History, JsonOptions),
                LastTransitionReason = s.LastTransitionReason,
                CreatedAtUtc = s.CreatedAtUtc,
                UpdatedAtUtc = s.UpdatedAtUtc,
                ExpiresAtUtc = expiresAt
            };

        private static StoredClueClickHistoryEntry MapToStoredClueClick(ClueClickHistoryEntry entry)
        {
            return new StoredClueClickHistoryEntry(
                ClueId: ClueCatalog.ToKey(entry.ClueId),
                IsFirstDiscovery: entry.IsFirstDiscovery,
                Source: entry.Source,
                ClueName: entry.ClueName,
                OccurredAtUtc: entry.OccurredAtUtc);
        }

        private static ClueClickHistoryEntry? MapToDomainClueClick(StoredClueClickHistoryEntry entry)
        {
            if (!ClueCatalog.TryParseKey(entry.ClueId, out var clueId))
            {
                return null;
            }

            return new ClueClickHistoryEntry(
                ClueId: clueId,
                IsFirstDiscovery: entry.IsFirstDiscovery,
                Source: entry.Source ?? string.Empty,
                ClueName: string.IsNullOrWhiteSpace(entry.ClueName)
                    ? ClueCatalog.ToDisplayName(clueId)
                    : entry.ClueName,
                OccurredAtUtc: entry.OccurredAtUtc);
        }

        private sealed record StoredClueClickHistoryEntry(
            string ClueId,
            bool IsFirstDiscovery,
            string Source,
            string ClueName,
            DateTimeOffset OccurredAtUtc);

        private static List<T> DeserializeList<T>(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<T>();
            }

            try
            {
                return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
            }
            catch (JsonException)
            {
                return new List<T>();
            }
        }
    }
}
