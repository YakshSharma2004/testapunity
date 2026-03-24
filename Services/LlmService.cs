using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testapi1.Application;
using testapi1.ApiContracts;
using testapi1.Services.Llm;

namespace testapi1.Services
{
    public sealed class LlmService : ILLMService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptionsMonitor<LlmOptions> _optionsMonitor;
        private readonly ILogger<LlmService> _logger;

        public LlmService(
            IHttpClientFactory httpClientFactory,
            IOptionsMonitor<LlmOptions> optionsMonitor,
            ILogger<LlmService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _optionsMonitor = optionsMonitor;
            _logger = logger;
        }

        public async Task<LlmRawResponse> GenerateResponseAsync(
            LlmPromptPayload payload,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(payload);

            var options = _optionsMonitor.CurrentValue;
            var localEnabled = options.Local.Enabled && IsConfigured(options.Local.BaseUrl, options.Local.Model);
            var remoteEnabled = options.Remote.Enabled && IsConfigured(options.Remote.BaseUrl, options.Remote.Model);

            if (!localEnabled && !remoteEnabled)
            {
                throw new InvalidOperationException(
                    "No LLM providers are configured. Set Llm:Local:Model for localhost inference or enable Llm:Remote.");
            }

            Exception? localFailure = null;

            if (localEnabled)
            {
                try
                {
                    var localResponse = await GenerateFromProviderAsync(
                        providerName: "local",
                        clientName: "llm-local",
                        baseUrl: options.Local.BaseUrl,
                        apiKey: string.Empty,
                        model: options.Local.Model,
                        timeoutMs: options.Local.TimeoutMs,
                        payload: payload,
                        defaults: options.Generation,
                        usedFallback: false,
                        cancellationToken: cancellationToken);

                    if (!payload.requireJson || IsJsonObject(localResponse.responseText))
                    {
                        return localResponse;
                    }

                    throw new InvalidOperationException("Local LLM returned a non-JSON response while JSON was required.");
                }
                catch (Exception ex) when (remoteEnabled)
                {
                    localFailure = ex;
                    _logger.LogWarning(
                        ex,
                        "Local LLM provider failed for conversation {ConversationId}. Falling back to remote provider.",
                        payload.conversationId);
                }
            }

            if (remoteEnabled)
            {
                try
                {
                    var remoteResponse = await GenerateFromProviderAsync(
                        providerName: "remote",
                        clientName: "llm-remote",
                        baseUrl: options.Remote.BaseUrl,
                        apiKey: options.Remote.ApiKey,
                        model: options.Remote.Model,
                        timeoutMs: options.Remote.TimeoutMs,
                        payload: payload,
                        defaults: options.Generation,
                        usedFallback: localFailure is not null,
                        cancellationToken: cancellationToken);

                    if (!payload.requireJson || IsJsonObject(remoteResponse.responseText))
                    {
                        return remoteResponse;
                    }

                    throw new InvalidOperationException("Remote LLM returned a non-JSON response while JSON was required.");
                }
                catch (Exception ex)
                {
                    if (localFailure is not null)
                    {
                        throw new AggregateException(
                            "Both local and remote LLM providers failed.",
                            localFailure,
                            ex);
                    }

                    throw;
                }
            }

            throw localFailure ?? new InvalidOperationException("Local LLM provider is enabled but failed unexpectedly.");
        }

        private async Task<LlmRawResponse> GenerateFromProviderAsync(
            string providerName,
            string clientName,
            string baseUrl,
            string apiKey,
            string model,
            int timeoutMs,
            LlmPromptPayload payload,
            LlmGenerationOptions defaults,
            bool usedFallback,
            CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient(clientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(baseUrl));

            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            }

            var body = new Dictionary<string, object?>
            {
                ["model"] = model,
                ["messages"] = BuildMessages(payload),
                ["max_tokens"] = payload.maxTokens > 0 ? payload.maxTokens : defaults.MaxTokens,
                ["temperature"] = payload.temperature ?? defaults.Temperature,
                ["stream"] = false
            };

            if (payload.requireJson)
            {
                body["response_format"] = new Dictionary<string, object?>
                {
                    ["type"] = "json_object"
                };
            }

            request.Content = JsonContent.Create(body, options: JsonOptions);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var effectiveTimeout = timeoutMs > 0 ? timeoutMs : 60000;
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(effectiveTimeout));

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);

            var responseBody = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"{providerName} LLM request failed: {(int)response.StatusCode} ({response.ReasonPhrase}). Body: {responseBody}");
            }

            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            var content = ExtractContent(root);

            return new LlmRawResponse
            {
                responseText = content,
                modelName = GetString(root, "model") ?? model,
                tokensUsed = ExtractTotalTokens(root),
                finishReason = ExtractFinishReason(root),
                provider = providerName,
                usedFallback = usedFallback
            };
        }

        private static object[] BuildMessages(LlmPromptPayload payload)
        {
            var messages = new List<object>();

            if (!string.IsNullOrWhiteSpace(payload.systemContext))
            {
                messages.Add(new Dictionary<string, object?>
                {
                    ["role"] = "system",
                    ["content"] = payload.systemContext
                });
            }

            messages.Add(new Dictionary<string, object?>
            {
                ["role"] = "user",
                ["content"] = payload.promptText ?? string.Empty
            });

            return messages.ToArray();
        }

        private static Uri BuildChatCompletionsUri(string baseUrl)
        {
            var trimmed = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new InvalidOperationException("LLM base URL must be configured.");
            }

            if (trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(trimmed, UriKind.Absolute);
            }

            if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri($"{trimmed}/chat/completions", UriKind.Absolute);
            }

            return new Uri($"{trimmed}/v1/chat/completions", UriKind.Absolute);
        }

        private static string ExtractContent(JsonElement root)
        {
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            {
                throw new InvalidOperationException("LLM response did not include any choices.");
            }

            var choice = choices[0];
            if (!choice.TryGetProperty("message", out var message))
            {
                throw new InvalidOperationException("LLM response did not include a message payload.");
            }

            if (!message.TryGetProperty("content", out var contentElement))
            {
                return string.Empty;
            }

            return ExtractMessageContent(contentElement);
        }

        private static string ExtractMessageContent(JsonElement contentElement)
        {
            return contentElement.ValueKind switch
            {
                JsonValueKind.String => contentElement.GetString() ?? string.Empty,
                JsonValueKind.Array => string.Concat(contentElement
                    .EnumerateArray()
                    .Select(item => item.ValueKind switch
                    {
                        JsonValueKind.String => item.GetString(),
                        JsonValueKind.Object when item.TryGetProperty("text", out var text) => text.GetString(),
                        _ => string.Empty
                    })),
                JsonValueKind.Object when contentElement.TryGetProperty("text", out var text) => text.GetString() ?? string.Empty,
                _ => contentElement.GetRawText()
            };
        }

        private static int ExtractTotalTokens(JsonElement root)
        {
            if (!root.TryGetProperty("usage", out var usage))
            {
                return 0;
            }

            return usage.TryGetProperty("total_tokens", out var totalTokens) && totalTokens.TryGetInt32(out var value)
                ? value
                : 0;
        }

        private static string ExtractFinishReason(JsonElement root)
        {
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            var choice = choices[0];
            return GetString(choice, "finish_reason") ?? string.Empty;
        }

        private static string? GetString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : property.GetRawText();
        }

        private static bool IsConfigured(string baseUrl, string model)
        {
            return !string.IsNullOrWhiteSpace(baseUrl) && !string.IsNullOrWhiteSpace(model);
        }

        private static bool IsJsonObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(text);
                return document.RootElement.ValueKind == JsonValueKind.Object;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
