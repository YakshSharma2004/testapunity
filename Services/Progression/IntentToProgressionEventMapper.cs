using testapi1.Application;
using testapi1.ApiContracts;
using testapi1.Domain.Progression;

namespace testapi1.Services.Progression
{
    public sealed class IntentToProgressionEventMapper : IIntentToProgressionEventMapper
    {
        private readonly IProgressionCatalogRepository _catalogRepository;

        public IntentToProgressionEventMapper(IProgressionCatalogRepository catalogRepository)
        {
            _catalogRepository = catalogRepository;
        }

        public async Task<ProgressionMappedEvent> MapAsync(
            IntentRequest request,
            IntentResponse response,
            string normalizedText,
            DateTimeOffset nowUtc,
            CancellationToken cancellationToken = default)
        {
            var action = await _catalogRepository.FindActionByIntentAsync(response.intent ?? string.Empty, cancellationToken);
            var eventType = action?.ProgressionEventType ?? ProgressionEventType.Unknown;
            var evidenceId = eventType == ProgressionEventType.PresentEvidence
                ? DetectEvidence(normalizedText)
                : null;

            var progressionEvent = new ProgressionEvent(
                EventType: eventType,
                Intent: response.intent ?? "unknown",
                Confidence: response.confidence,
                RawText: request.Text ?? string.Empty,
                NormalizedText: normalizedText ?? string.Empty,
                EvidenceId: evidenceId,
                OccurredAtUtc: nowUtc);

            return new ProgressionMappedEvent(
                Event: progressionEvent,
                ActionId: action?.ActionId);
        }

        public Task<IReadOnlyList<string>> GetAllowedIntentsAsync(
            ProgressionStateId state,
            CancellationToken cancellationToken = default)
        {
            return _catalogRepository.GetAllowedIntentCodesAsync(state, cancellationToken);
        }

        private static EvidenceId? DetectEvidence(string normalizedText)
        {
            var text = normalizedText ?? string.Empty;

            if (ContainsAny(text, "pawn", "receipt", "marrow", "finch"))
            {
                return EvidenceId.E5PawnReceipt;
            }

            if (ContainsAny(text, "alarm", "disarmed", "rearmed", "2 13", "2:13"))
            {
                return EvidenceId.E4AlarmLog;
            }

            if (ContainsAny(text, "safe", "no damage", "pry mark", "pry marks"))
            {
                return EvidenceId.E2SafeNoDamage;
            }

            if (ContainsAny(text, "glass", "inside the study", "window"))
            {
                return EvidenceId.E1WindowGlassPattern;
            }

            if (ContainsAny(text, "debt", "final notice", "february", "feb 2", "february second"))
            {
                return EvidenceId.E7DebtNotice;
            }

            return null;
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            foreach (var token in tokens)
            {
                if (value.Contains(token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
