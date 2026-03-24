namespace testapi1.Domain.Progression
{
    public enum ClueId
    {
        ElsaEmailDraft = 1,
        PayrollReport = 2,
        MeetingNote = 3,
        EntryLog = 4,
        PinNote = 5,
        CleanupItem = 6,
        LockedSuitcaseOpened = 7,
        WeaponFound = 8,
        FlashDrive = 9
    }

    public enum ComposureState
    {
        Calm = 0,
        Guarded = 1,
        Defensive = 2,
        Cracking = 3,
        Broken = 4
    }

    public enum ProofTier
    {
        None = 0,
        Minimum = 1,
        Full = 2
    }

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

    public sealed record ClueClickHistoryEntry(
        ClueId ClueId,
        bool IsFirstDiscovery,
        string Source,
        string ClueName,
        DateTimeOffset OccurredAtUtc);

    public sealed record ProgressionSessionState(
        string SessionId,
        int PlayerId,
        string CaseId,
        string NpcId,
        ProgressionStateId State,
        int TurnCount,
        int TrustScore,
        int ShutdownScore,
        bool IsTerminal,
        CaseEndingType Ending,
        IReadOnlyCollection<EvidenceId> PresentedEvidence,
        IReadOnlyCollection<ClueId> DiscoveredClues,
        IReadOnlyCollection<ClueId> DiscussedClues,
        IReadOnlyList<ClueClickHistoryEntry> ClueClickHistory,
        ComposureState ComposureState,
        ProofTier ProofTier,
        bool CanConfess,
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
