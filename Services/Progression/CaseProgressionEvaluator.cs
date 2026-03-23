using testapi1.Domain.Progression;

namespace testapi1.Services.Progression
{
    internal static class CaseProgressionEvaluator
    {
        private static readonly ClueId[] MinimumProofClues =
        {
            ClueId.ElsaEmailDraft,
            ClueId.PayrollReport,
            ClueId.EntryLog,
            ClueId.WeaponFound
        };

        private static readonly ClueId[] FullProofAdditionalClues =
        {
            ClueId.MeetingNote,
            ClueId.CleanupItem,
            ClueId.FlashDrive
        };

        public static ProgressionSessionState Recalculate(
            ProgressionSessionState state,
            IReadOnlyCollection<ClueId> requiredForConfession)
        {
            var discovered = state.DiscoveredClues.ToHashSet();
            var discussed = state.DiscussedClues.ToHashSet();

            var composure = ResolveComposure(discovered);
            var proofTier = ResolveProofTier(discovered);
            var canConfess = requiredForConfession.All(clue => discovered.Contains(clue) && discussed.Contains(clue));

            return state with
            {
                ComposureState = composure,
                ProofTier = proofTier,
                CanConfess = canConfess
            };
        }

        public static ComposureState ResolveComposure(IReadOnlySet<ClueId> discoveredClues)
        {
            if (discoveredClues.Contains(ClueId.WeaponFound) || discoveredClues.Contains(ClueId.FlashDrive))
            {
                return ComposureState.Broken;
            }

            if (discoveredClues.Contains(ClueId.PinNote) || discoveredClues.Contains(ClueId.LockedSuitcaseOpened))
            {
                return ComposureState.Cracking;
            }

            if (discoveredClues.Contains(ClueId.MeetingNote) || discoveredClues.Contains(ClueId.EntryLog))
            {
                return ComposureState.Defensive;
            }

            if (discoveredClues.Contains(ClueId.ElsaEmailDraft) || discoveredClues.Contains(ClueId.PayrollReport))
            {
                return ComposureState.Guarded;
            }

            return ComposureState.Calm;
        }

        public static ProofTier ResolveProofTier(IReadOnlySet<ClueId> discoveredClues)
        {
            var hasMinimum = MinimumProofClues.All(discoveredClues.Contains);
            if (!hasMinimum)
            {
                return ProofTier.None;
            }

            var hasFull = FullProofAdditionalClues.All(discoveredClues.Contains);
            return hasFull ? ProofTier.Full : ProofTier.Minimum;
        }
    }
}
