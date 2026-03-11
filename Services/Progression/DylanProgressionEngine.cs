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
                "pawn-receipt-confession-window",
                Guard: static (state, ev) =>
                    ev.EvidenceId == EvidenceId.E5PawnReceipt &&
                    HasEvidence(state, EvidenceId.E1WindowGlassPattern, EvidenceId.E2SafeNoDamage, EvidenceId.E4AlarmLog, EvidenceId.E5PawnReceipt) &&
                    state.TrustScore >= state.ShutdownScore),

            new(
                ProgressionStateId.ConfessionWindow,
                ProgressionEventType.AskOpenQuestion,
                ProgressionStateId.Confession,
                "confession-open-question",
                Guard: static (state, _) => state.PresentedEvidence.Contains(EvidenceId.E5PawnReceipt)),
            new(
                ProgressionStateId.ConfessionWindow,
                ProgressionEventType.Empathy,
                ProgressionStateId.Confession,
                "confession-empathy",
                Guard: static (state, _) => state.PresentedEvidence.Contains(EvidenceId.E5PawnReceipt)),
            new(
                ProgressionStateId.ConfessionWindow,
                ProgressionEventType.Silence,
                ProgressionStateId.Confession,
                "confession-silence",
                Guard: static (state, _) => state.PresentedEvidence.Contains(EvidenceId.E5PawnReceipt))
        };

        public ProgressionSessionState CreateInitialState(string sessionId, string caseId, string npcId, DateTimeOffset nowUtc)
        {
            return new ProgressionSessionState(
                SessionId: sessionId,
                CaseId: string.IsNullOrWhiteSpace(caseId) ? "dylan-interrogation" : caseId,
                NpcId: string.IsNullOrWhiteSpace(npcId) ? "dylan" : npcId,
                State: ProgressionStateId.Intro,
                TurnCount: 0,
                TrustScore: 0,
                ShutdownScore: 0,
                IsTerminal: false,
                Ending: CaseEndingType.None,
                PresentedEvidence: new List<EvidenceId>(),
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

        public IReadOnlyList<string> GetAllowedIntents(ProgressionSessionState state)
        {
            if (state.IsTerminal || TerminalStates.Contains(state.State))
            {
                return Array.Empty<string>();
            }

            return state.State switch
            {
                ProgressionStateId.Intro => new[]
                {
                    "ASK_OPEN_QUESTION",
                    "ASK_TIMELINE",
                    "EMPATHY",
                    "PRESENT_EVIDENCE",
                    "CONTRADICTION",
                    "INTIMIDATE",
                    "SILENCE"
                },
                ProgressionStateId.InformationGathering => new[]
                {
                    "ASK_OPEN_QUESTION",
                    "ASK_TIMELINE",
                    "EMPATHY",
                    "PRESENT_EVIDENCE",
                    "SILENCE"
                },
                ProgressionStateId.BuildingCase => new[]
                {
                    "ASK_OPEN_QUESTION",
                    "ASK_TIMELINE",
                    "PRESENT_EVIDENCE",
                    "CONTRADICTION",
                    "EMPATHY",
                    "SILENCE",
                    "INTIMIDATE",
                    "CLOSE_INTERROGATION"
                },
                ProgressionStateId.ConfessionWindow => new[]
                {
                    "ASK_OPEN_QUESTION",
                    "EMPATHY",
                    "SILENCE",
                    "CONTRADICTION",
                    "CLOSE_INTERROGATION"
                },
                _ => Array.Empty<string>()
            };
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
                if (HasEvidence(state, EvidenceId.E1WindowGlassPattern, EvidenceId.E2SafeNoDamage, EvidenceId.E4AlarmLog, EvidenceId.E5PawnReceipt))
                {
                    transitioned = state.State != ProgressionStateId.GuiltyNoConfession;
                    reason = "evidence-chain-complete-no-confession";
                    return state with { State = ProgressionStateId.GuiltyNoConfession };
                }

                transitioned = state.State != ProgressionStateId.ClosedNoResolution;
                reason = "interrogation-closed-without-resolution";
                return state with { State = ProgressionStateId.ClosedNoResolution };
            }

            if (progressionEvent.EventType == ProgressionEventType.Intimidate &&
                state.ShutdownScore >= 3 &&
                HasEvidence(state, EvidenceId.E1WindowGlassPattern, EvidenceId.E2SafeNoDamage, EvidenceId.E4AlarmLog, EvidenceId.E5PawnReceipt))
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

        private static bool HasEvidence(ProgressionSessionState state, params EvidenceId[] evidenceIds)
        {
            var presented = state.PresentedEvidence.ToHashSet();
            return evidenceIds.All(presented.Contains);
        }
    }
}
