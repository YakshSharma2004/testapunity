using Microsoft.EntityFrameworkCore;
using testapi1.Application;
using testapi1.Domain;

namespace testapi1.Infrastructure.Persistence
{
    public sealed class PostgresProgressionRuntimeRepository : IProgressionRuntimeRepository
    {
        private readonly AppDbContext _db;

        public PostgresProgressionRuntimeRepository(AppDbContext db)
        {
            _db = db;
        }

        public Task<bool> PlayerExistsAsync(int playerId, CancellationToken cancellationToken = default)
        {
            return _db.Players.AsNoTracking().AnyAsync(item => item.PlayerId == playerId, cancellationToken);
        }

        public async Task<NpcRuntimeIdentity?> GetNpcByCodeAsync(
            string npcCode,
            CancellationToken cancellationToken = default)
        {
            var trimmed = (npcCode ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return null;
            }

            var normalized = trimmed.ToLowerInvariant();
            var npc = await _db.Npcs
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.NpcCode == normalized, cancellationToken)
                ?? await _db.Npcs.AsNoTracking()
                    .FirstOrDefaultAsync(item => item.NpcCode == trimmed, cancellationToken);

            return npc is null
                ? null
                : new NpcRuntimeIdentity(npc.NpcId, npc.NpcCode, npc.Name);
        }

        public async Task EnsurePlayerNpcStateAsync(
            int playerId,
            int npcId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            var existing = await _db.PlayerNpcStates.FindAsync(new object[] { playerId, npcId }, cancellationToken);
            if (existing is not null)
            {
                return;
            }

            _db.PlayerNpcStates.Add(new PlayerNpcState
            {
                PlayerId = playerId,
                NpcId = npcId,
                Trust = 0.50m,
                Patience = 0.50m,
                Curiosity = 0.50m,
                Openness = 0.50m,
                Memory = "initialized",
                LastInteractionAt = nowUtc.UtcDateTime
            });

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task PersistTurnAsync(
            TurnPersistenceRecord record,
            CancellationToken cancellationToken = default)
        {
            var occurredAtUtc = record.OccurredAtUtc.UtcDateTime;

            _db.Interactions.Add(new Interaction
            {
                PlayerId = record.PlayerId,
                NpcId = record.NpcId,
                OccurredAt = occurredAtUtc,
                Location = "interrogation-room",
                PlayerAction = record.EventType,
                PlayerText = record.PlayerText,
                NluTopIntent = record.IntentCode,
                Sentiment = 0.00m,
                Friendliness = NormalizeScore(record.TrustScore),
                ToneTag = record.ComposureState.ToLowerInvariant(),
                NsfwFlag = false,
                ChosenActionId = record.ActionId,
                ResponseText = record.TransitionReason,
                ResponseSource = "PROGRESSION_ENGINE",
                ModelVersion = string.IsNullOrWhiteSpace(record.ModelVersion) ? "unknown" : record.ModelVersion,
                RewardScore = 0.0000m,
                OutcomeFlags = $"turn:{record.TurnCount};reason:{record.TransitionReason}"
            });

            var state = await _db.PlayerNpcStates.FindAsync(new object[] { record.PlayerId, record.NpcId }, cancellationToken);
            if (state is null)
            {
                state = new PlayerNpcState
                {
                    PlayerId = record.PlayerId,
                    NpcId = record.NpcId
                };
                _db.PlayerNpcStates.Add(state);
            }

            var trust = NormalizeScore(record.TrustScore);
            var patience = NormalizeScore(10 - record.ShutdownScore);
            var curiosity = Math.Clamp(0.50m + (record.TurnCount * 0.02m), 0m, 1m);
            var openness = Math.Clamp((trust + patience) / 2m, 0m, 1m);

            state.Trust = trust;
            state.Patience = patience;
            state.Curiosity = curiosity;
            state.Openness = openness;
            state.Memory = $"intent={record.IntentCode};reason={record.TransitionReason};turn={record.TurnCount}";
            state.LastInteractionAt = occurredAtUtc;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task TouchPlayerNpcStateAsync(
            int playerId,
            int npcId,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            var state = await _db.PlayerNpcStates.FindAsync(new object[] { playerId, npcId }, cancellationToken);
            if (state is null)
            {
                state = new PlayerNpcState
                {
                    PlayerId = playerId,
                    NpcId = npcId,
                    Trust = 0.50m,
                    Patience = 0.50m,
                    Curiosity = 0.50m,
                    Openness = 0.50m,
                    Memory = "initialized",
                    LastInteractionAt = nowUtc.UtcDateTime
                };
                _db.PlayerNpcStates.Add(state);
            }
            else
            {
                state.LastInteractionAt = nowUtc.UtcDateTime;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        private static decimal NormalizeScore(int score)
        {
            return Math.Clamp(score, 0, 10) / 10m;
        }
    }
}
