using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using testapi1.Application;

namespace testapi1.Services
{
    public class CacheInvalidationTokenSource : ICacheInvalidationTokenSource, IDisposable
    {
        private readonly ILogger<CacheInvalidationTokenSource> _logger;
        private readonly IDisposable _changeSubscription;
        private CancellationTokenSource _cts = new();
        private string _modelVersion;

        public CacheInvalidationTokenSource(
            IOptionsMonitor<ApiCacheOptions> optionsMonitor,
            ILogger<CacheInvalidationTokenSource> logger)
        {
            _logger = logger;
            _modelVersion = optionsMonitor.CurrentValue.ModelVersion ?? "";
            _changeSubscription = optionsMonitor.OnChange(OnOptionsChanged);
        }

        public IChangeToken GetChangeToken()
        {
            return new CancellationChangeToken(_cts.Token);
        }

        private void OnOptionsChanged(ApiCacheOptions options)
        {
            var nextVersion = options.ModelVersion ?? "";
            if (string.Equals(nextVersion, _modelVersion, StringComparison.Ordinal))
            {
                return;
            }

            _logger.LogInformation("Model version changed from {Previous} to {Next}. Clearing cache entries.", _modelVersion, nextVersion);
            _modelVersion = nextVersion;
            var previousCts = _cts;
            _cts = new CancellationTokenSource();
            previousCts.Cancel();
            previousCts.Dispose();
        }

        public void Dispose()
        {
            _changeSubscription.Dispose();
            _cts.Dispose();
        }
    }
}
