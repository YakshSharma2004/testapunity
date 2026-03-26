using testapi1.Domain.Progression;

namespace testapi1.Application
{
    public interface IGameProgressionEngine
    {
        ProgressionSessionState CreateInitialState(string sessionId, int playerId, string caseId, string npcId, DateTimeOffset nowUtc);
        ProgressionTransitionResult Apply(ProgressionSessionState currentState, ProgressionEvent progressionEvent);
    }
}
