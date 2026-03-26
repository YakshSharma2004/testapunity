using System.Linq;
using testapi1.Application;
using testapi1.Domain.Progression;

namespace testapi1.Services.Progression
{
    public sealed class DylanProgressionEngine : IGameProgressionEngine
    {
        private static readonly HashSet<ProgressionStateId> TerminalStates = new()
        {
            ProgressionStateId.Confession,
            ProgressionStateId.GuiltyNoConfession,
            ProgressionStateId.ClosedNoResolution
        };

        private static readonly IReadOnlyList<TransitionRule> Rules = new List<TransitionRule>
        {
            new(
                ProgressionStateId.Intro,
                ProgressionEventType.AskOpenQuestion,
                ProgressionStateId.InformationGathering,
                "opening-question"),
            new(
                ProgressionStateId.Intro,
                ProgressionEventType.AskTimeline,
                ProgressionStateId.InformationGathering,
                "timeline-open"),
            new(
                ProgressionStateId.Intro,
                ProgressionEventType.Empathy,
                ProgressionStateId.InformationGathering,
                "rapport-start"),
            new(
                ProgressionStateId.Intro,
                ProgressionEventType.PresentEvidence,
                ProgressionStateId.BuildingCase,
                "early-evidence"),
            new(
                ProgressionStateId.Intro,
                ProgressionEventType.Contradiction,
                ProgressionStateId.InformationGathering,
                "challenge-start"),
            new(
                ProgressionStateId.Intro,
                ProgressionEventType.Intimidate,
                ProgressionStateId.InformationGathering,
                "pressure-start"),

            new(
                ProgressionStateId.InformationGathering,
                ProgressionEventType.PresentEvidence,
                ProgressionStateId.BuildingCase,
                "evidence-phase-start"),

            new(
                ProgressionStateId.BuildingCase,
                ProgressionEventType.PresentEvidence,
                ProgressionStateId.ConfessionWindow,
                "evidence-to-confession-window"),

            new(
                ProgressionStateId.ConfessionWindow,
                ProgressionEventType.AskOpenQuestion,
                ProgressionStateId.Confession,
                "confession-open-question",
                Guard: static (state, _) => state.CanConfess),
            new(
                ProgressionStateId.ConfessionWindow,
                ProgressionEventType.Empathy,
                ProgressionStateId.Confession,
                "confession-empathy",
                Guard: static (state, _) => state.CanConfess),
            new(
                ProgressionStateId.ConfessionWindow,
                ProgressionEventType.Silence,
                ProgressionStateId.Confession,
                "confession-silence",
                Guard: static (state, _) => state.CanConfess)
        };

        public ProgressionSessionState CreateInitialState(string sessionId, int playerId, string caseId, string npcId, DateTimeOffset nowUtc)
        {
            return new ProgressionSessionState(
                SessionId: sessionId,
                PlayerId: playerId,
                CaseId: string.IsNullOrWhiteSpace(caseId) ? "dylan-interrogation" : caseId,
                NpcId: string.IsNullOrWhiteSpace(npcId) ? "dylan" : npcId,
                State: ProgressionStateId.Intro,
                TurnCount: 0,
                TrustScore: 0,
                ShutdownScore: 0,
                IsTerminal: false,
                Ending: CaseEndingType.None,
                PresentedEvidence: new List<EvidenceId>(),
                DiscoveredClues: new List<ClueId>(),
                DiscussedClues: new List<ClueId>(),
                ClueClickHistory: new List<ClueClickHistoryEntry>(),
                ComposureState: ComposureState.Calm,
                ProofTier: ProofTier.None,
                CanConfess: false,
                History: new List<ProgressionHistoryEntry>(),
                LastTransitionReason: "session-started",
                CreatedAtUtc: nowUtc,
                UpdatedAtUtc: nowUtc);
        }

        public ProgressionTransitionResult Apply(ProgressionSessionState currentState, ProgressionEvent progressionEvent)
        {
            if (currentState.IsTerminal || TerminalStates.Contains(currentState.State))
            {
                var terminalState = currentState with
                {
                    IsTerminal = true,
                    LastTransitionReason = "session-already-terminal",
                    UpdatedAtUtc = progressionEvent.OccurredAtUtc
                };

                return new ProgressionTransitionResult(terminalState, false, terminalState.LastTransitionReason);
            }

            var withEffects = ApplyEventEffects(currentState, progressionEvent);
            var fromState = withEffects.State;
            var nextState = withEffects;
            var reason = "no-transition";
            var transitioned = false;

            var rule = Rules.FirstOrDefault(item =>
                item.FromState == withEffects.State &&
                item.EventType == progressionEvent.EventType &&
                (item.Guard == null || item.Guard(withEffects, progressionEvent)));

            if (rule is not null)
            {
                nextState = rule.Apply is not null
                    ? rule.Apply(withEffects, progressionEvent)
                    : withEffects;

                transitioned = rule.ToState != withEffects.State;
                reason = rule.Reason;
                nextState = nextState with { State = rule.ToState };
            }

            nextState = ApplyTerminalOutcomes(nextState, progressionEvent, ref transitioned, ref reason);
            nextState = NormalizeTerminalState(nextState);

            var history = nextState.History.ToList();
            history.Add(new ProgressionHistoryEntry(
                Turn: nextState.TurnCount,
                FromState: fromState,
                ToState: nextState.State,
                EventType: progressionEvent.EventType,
                Intent: progressionEvent.Intent,
                EvidenceId: progressionEvent.EvidenceId,
                Reason: reason,
                OccurredAtUtc: progressionEvent.OccurredAtUtc));

            nextState = nextState with
            {
                History = history,
                LastTransitionReason = reason,
                UpdatedAtUtc = progressionEvent.OccurredAtUtc
            };

            return new ProgressionTransitionResult(nextState, transitioned, reason);
        }

        private static ProgressionSessionState ApplyEventEffects(ProgressionSessionState state, ProgressionEvent progressionEvent)
        {
            var trust = state.TrustScore;
            var shutdown = state.ShutdownScore;

            switch (progressionEvent.EventType)
            {
                case ProgressionEventType.Empathy:
                    trust += 1;
                    shutdown -= 1;
                    break;
                case ProgressionEventType.Intimidate:
                    trust -= 1;
                    shutdown += 2;
                    break;
                case ProgressionEventType.Contradiction:
                    shutdown += 1;
                    break;
                case ProgressionEventType.AskOpenQuestion:
                    trust += 1;
                    break;
            }

            trust = Math.Clamp(trust, 0, 10);
            shutdown = Math.Clamp(shutdown, 0, 10);

            var evidence = state.PresentedEvidence.ToHashSet();
            if (progressionEvent.EvidenceId.HasValue)
            {
                evidence.Add(progressionEvent.EvidenceId.Value);
            }

            return state with
            {
                TurnCount = state.TurnCount + 1,
                TrustScore = trust,
                ShutdownScore = shutdown,
                PresentedEvidence = evidence.ToList()
            };
        }

        private static ProgressionSessionState ApplyTerminalOutcomes(
            ProgressionSessionState state,
            ProgressionEvent progressionEvent,
            ref bool transitioned,
            ref string reason)
        {
            if (state.State == ProgressionStateId.Confession)
            {
                return state;
            }

            if (progressionEvent.EventType == ProgressionEventType.CloseInterrogation)
            {
                if (state.ProofTier is ProofTier.Minimum or ProofTier.Full)
                {
                    transitioned = state.State != ProgressionStateId.GuiltyNoConfession;
                    reason = "proof-threshold-met-no-confession";
                    return state with { State = ProgressionStateId.GuiltyNoConfession };
                }

                transitioned = state.State != ProgressionStateId.ClosedNoResolution;
                reason = "interrogation-closed-without-resolution";
                return state with { State = ProgressionStateId.ClosedNoResolution };
            }

            if (progressionEvent.EventType == ProgressionEventType.Intimidate &&
                state.ShutdownScore >= 3 &&
                state.ProofTier is ProofTier.Minimum or ProofTier.Full)
            {
                transitioned = state.State != ProgressionStateId.GuiltyNoConfession;
                reason = "shutdown-triggered-no-confession-ending";
                return state with { State = ProgressionStateId.GuiltyNoConfession };
            }

            return state;
        }

        private static ProgressionSessionState NormalizeTerminalState(ProgressionSessionState state)
        {
            if (state.State == ProgressionStateId.Confession)
            {
                return state with
                {
                    IsTerminal = true,
                    Ending = CaseEndingType.Confession
                };
            }

            if (state.State == ProgressionStateId.GuiltyNoConfession)
            {
                return state with
                {
                    IsTerminal = true,
                    Ending = CaseEndingType.GuiltyNoConfession
                };
            }

            if (state.State == ProgressionStateId.ClosedNoResolution)
            {
                return state with
                {
                    IsTerminal = true,
                    Ending = CaseEndingType.ClosedNoResolution
                };
            }

            return state with
            {
                IsTerminal = false,
                Ending = CaseEndingType.None
            };
        }

    }
}
