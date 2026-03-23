using testapi1.Application;
using testapi1.ApiContracts;
using testapi1.Domain.Progression;

namespace testapi1.Services.Progression
{
    public sealed class IntentToProgressionEventMapper : IIntentToProgressionEventMapper
    {
        public ProgressionEvent Map(IntentRequest request, IntentResponse response, string normalizedText, DateTimeOffset nowUtc)
        {
            var eventType = MapIntentToEventType(response.intent);
            var evidenceId = eventType == ProgressionEventType.PresentEvidence
                ? DetectEvidence(normalizedText)
                : null;

            return new ProgressionEvent(
                EventType: eventType,
                Intent: response.intent ?? "unknown",
                Confidence: response.confidence,
                RawText: request.Text ?? string.Empty,
                NormalizedText: normalizedText ?? string.Empty,
                EvidenceId: evidenceId,
                OccurredAtUtc: nowUtc);
        }

        private static ProgressionEventType MapIntentToEventType(string? intent)
        {
            return (intent ?? string.Empty).Trim().ToUpperInvariant() switch
            {
                "ASK_OPEN_QUESTION" => ProgressionEventType.AskOpenQuestion,
                "ASK_TIMELINE" => ProgressionEventType.AskTimeline,
                "EMPATHY" => ProgressionEventType.Empathy,
                "PRESENT_EVIDENCE" => ProgressionEventType.PresentEvidence,
                "CONTRADICTION" => ProgressionEventType.Contradiction,
                "SILENCE" => ProgressionEventType.Silence,
                "INTIMIDATE" => ProgressionEventType.Intimidate,
                "CLOSE_INTERROGATION" => ProgressionEventType.CloseInterrogation,
                _ => ProgressionEventType.Unknown
            };
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
