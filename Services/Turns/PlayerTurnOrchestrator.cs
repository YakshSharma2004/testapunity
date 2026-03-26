using testapi1.ApiContracts;
using testapi1.Application;

namespace testapi1.Services.Turns
{
    public sealed class PlayerTurnOrchestrator : IPlayerTurnOrchestrator
    {
        private readonly IProgressionSessionStore _sessionStore;
        private readonly IPlayerTurnResolver _turnResolver;
        private readonly IResolvedProgressionTurnService _progressionService;
        private readonly IResolvedNpcDialogueService _npcDialogueService;

        public PlayerTurnOrchestrator(
            IProgressionSessionStore sessionStore,
            IPlayerTurnResolver turnResolver,
            IResolvedProgressionTurnService progressionService,
            IResolvedNpcDialogueService npcDialogueService)
        {
            _sessionStore = sessionStore;
            _turnResolver = turnResolver;
            _progressionService = progressionService;
            _npcDialogueService = npcDialogueService;
        }

        public async Task<ProgressionTurnResponse?> ApplyAsync(
            ProgressionTurnRequest request,
            CancellationToken cancellationToken = default)
        {
            var session = await _sessionStore.GetAsync(request.sessionId, cancellationToken);
            if (session is null)
            {
                return null;
            }

            var resolvedTurn = await _turnResolver.ResolveAsync(request, session.NpcId, cancellationToken);
            var progression = await _progressionService.ApplyResolvedTurnAsync(resolvedTurn, cancellationToken);
            if (progression is null)
            {
                return null;
            }

            var dialogue = await _npcDialogueService.GenerateAsync(
                new NpcDialogueRequest
                {
                    sessionId = resolvedTurn.SessionId,
                    text = resolvedTurn.Text,
                    contextKey = resolvedTurn.ContextKey
                },
                resolvedTurn,
                progression.PersistedTurn,
                cancellationToken);

            if (dialogue is null)
            {
                throw new InvalidOperationException(
                    $"Unable to generate NPC dialogue for session '{resolvedTurn.SessionId}'.");
            }

            progression.Response.replyText = dialogue.replyText;
            return progression.Response;
        }
    }
}
