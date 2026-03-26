using Microsoft.EntityFrameworkCore;
using testapi1.Application;
using testapi1.Domain.Progression;

namespace testapi1.Infrastructure.Persistence
{
    public sealed class PostgresProgressionCatalogRepository : IProgressionCatalogRepository
    {
        private readonly AppDbContext _db;

        public PostgresProgressionCatalogRepository(AppDbContext db)
        {
            _db = db;
        }

        public async Task<ProgressionCatalogAction?> FindActionByIntentAsync(
            string intentCode,
            CancellationToken cancellationToken = default)
        {
            var normalized = (intentCode ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var action = await _db.ActionCatalog
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    item => item.IsEnabled &&
                            (item.Code == normalized || item.IntentTag == normalized),
                    cancellationToken);

            if (action is null)
            {
                return null;
            }

            var eventType = Enum.TryParse<ProgressionEventType>(action.ProgressionEventType, out var parsed)
                ? parsed
                : ProgressionEventType.Unknown;

            return new ProgressionCatalogAction(
                ActionId: action.ActionId,
                Code: action.Code,
                IntentTag: action.IntentTag,
                ProgressionEventType: eventType,
                IsEnabled: action.IsEnabled);
        }

        public async Task<IReadOnlyList<string>> GetAllowedIntentCodesAsync(
            ProgressionStateId state,
            CancellationToken cancellationToken = default)
        {
            var stateKey = state.ToString();

            var codes = await _db.ProgressionStateAllowedActions
                .AsNoTracking()
                .Where(item => item.State == stateKey && item.Action.IsEnabled)
                .Select(item => item.Action.Code)
                .Distinct()
                .OrderBy(item => item)
                .ToListAsync(cancellationToken);

            return codes;
        }
    }
}
