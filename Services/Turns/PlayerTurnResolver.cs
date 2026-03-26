using testapi1.ApiContracts;
using testapi1.Application;

namespace testapi1.Services.Turns
{
    public sealed class PlayerTurnResolver : IPlayerTurnResolver
    {
        private readonly ITextNormalizer _normalizer;
        private readonly IIntentClassifier _intentClassifier;

        public PlayerTurnResolver(
            ITextNormalizer normalizer,
            IIntentClassifier intentClassifier)
        {
            _normalizer = normalizer;
            _intentClassifier = intentClassifier;
        }

        public async Task<ResolvedPlayerTurn> ResolveAsync(
            ProgressionTurnRequest request,
            string npcId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var rawText = request.text ?? string.Empty;
            var resolvedNpcId = string.IsNullOrWhiteSpace(request.npcId) ? npcId : request.npcId.Trim().ToLowerInvariant();
            var contextKey = request.contextKey ?? string.Empty;
            var normalizedText = _normalizer.NormalizeForMatch(rawText);
            var intent = await _intentClassifier.ClassifyAsync(
                new IntentRequest
                {
                    Text = rawText,
                    NpcId = resolvedNpcId,
                    ContextKey = contextKey
                },
                cancellationToken);

            return new ResolvedPlayerTurn(
                SessionId: request.sessionId ?? string.Empty,
                Text: rawText,
                NpcId: resolvedNpcId,
                ContextKey: contextKey,
                DiscussedClueIds: request.discussedClueIds
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                NormalizedText: normalizedText,
                Intent: intent);
        }
    }
}
