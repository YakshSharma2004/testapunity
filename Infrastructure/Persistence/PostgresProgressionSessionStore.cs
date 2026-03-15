
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
                existing.State = state.State.ToString();
                existing.TurnCount = state.TurnCount;
                existing.TrustScore = state.TrustScore;
                existing.ShutdownScore = state.ShutdownScore;
                existing.IsTerminal = state.IsTerminal;
                existing.Ending = state.Ending.ToString();
                existing.PresentedEvidenceJson = JsonSerializer.Serialize(
                    state.PresentedEvidence.Select(e => e.ToString()));
                existing.HistoryJson = JsonSerializer.Serialize(state.History);
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
            var evidence = JsonSerializer.Deserialize<List<string>>(e.PresentedEvidenceJson) ?? new();
            var history = JsonSerializer.Deserialize<List<ProgressionHistoryEntry>>(e.HistoryJson) ?? new();

            return new ProgressionSessionState(
                SessionId: e.SessionId,
                CaseId: e.CaseId,
                NpcId: e.NpcId,
                State: Enum.Parse<ProgressionStateId>(e.State),
                TurnCount: e.TurnCount,
                TrustScore: e.TrustScore,
                ShutdownScore: e.ShutdownScore,
                IsTerminal: e.IsTerminal,
                Ending: Enum.Parse<CaseEndingType>(e.Ending),
                PresentedEvidence: evidence.Select(Enum.Parse<EvidenceId>).ToList(),
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
                CaseId = s.CaseId,
                NpcId = s.NpcId,
                State = s.State.ToString(),
                TurnCount = s.TurnCount,
                TrustScore = s.TrustScore,
                ShutdownScore = s.ShutdownScore,
                IsTerminal = s.IsTerminal,
                Ending = s.Ending.ToString(),
                PresentedEvidenceJson = JsonSerializer.Serialize(
                s.PresentedEvidence.Select(e => e.ToString())),
                HistoryJson = JsonSerializer.Serialize(s.History),
                LastTransitionReason = s.LastTransitionReason,
                CreatedAtUtc = s.CreatedAtUtc,
                UpdatedAtUtc = s.UpdatedAtUtc,
                ExpiresAtUtc = expiresAt
            };
    }
}
