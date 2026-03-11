namespace testapi1.Domain.Progression
{
    public enum ProgressionStateId
    {
        Intro = 0,
        InformationGathering = 1,
        BuildingCase = 2,
        ConfessionWindow = 3,
        Confession = 4,
        GuiltyNoConfession = 5,
        ClosedNoResolution = 6
    }

    public enum ProgressionEventType
    {
        AskOpenQuestion = 0,
        AskTimeline = 1,
        Empathy = 2,
        PresentEvidence = 3,
        Contradiction = 4,
        Silence = 5,
        Intimidate = 6,
        CloseInterrogation = 7,
        Unknown = 8
    }

    public enum EvidenceId
    {
        E1WindowGlassPattern = 1,
        E2SafeNoDamage = 2,
        E4AlarmLog = 4,
        E5PawnReceipt = 5,
        E7DebtNotice = 7
    }

    public enum CaseEndingType
    {
        None = 0,
        Confession = 1,
        GuiltyNoConfession = 2,
        ClosedNoResolution = 3
    }

    public sealed record ProgressionEvent(
        ProgressionEventType EventType,
        string Intent,
        float Confidence,
        string RawText,
        string NormalizedText,
        EvidenceId? EvidenceId,
        DateTimeOffset OccurredAtUtc);

    public sealed record ProgressionHistoryEntry(
        int Turn,
        ProgressionStateId FromState,
        ProgressionStateId ToState,
        ProgressionEventType EventType,
        string Intent,
        EvidenceId? EvidenceId,
        string Reason,
        DateTimeOffset OccurredAtUtc);

    public sealed record ProgressionSessionState(
        string SessionId,
        string CaseId,
        string NpcId,
        ProgressionStateId State,
        int TurnCount,
        int TrustScore,
        int ShutdownScore,
        bool IsTerminal,
        CaseEndingType Ending,
        IReadOnlyCollection<EvidenceId> PresentedEvidence,
        IReadOnlyList<ProgressionHistoryEntry> History,
        string LastTransitionReason,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc);

    public sealed record ProgressionTransitionResult(
        ProgressionSessionState State,
        bool Transitioned,
        string Reason);

    public sealed record TransitionRule(
        ProgressionStateId FromState,
        ProgressionEventType EventType,
        ProgressionStateId ToState,
        string Reason,
        Func<ProgressionSessionState, ProgressionEvent, bool>? Guard = null,
        Func<ProgressionSessionState, ProgressionEvent, ProgressionSessionState>? Apply = null);
}
