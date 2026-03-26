using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using testapi1.ApiContracts;
using testapi1.Services;
using testapi1.Services.Llm;
using testapi1.Tests.TestSupport;

namespace testapi1.Tests
{
    public sealed class LlmServiceTests
    {
        [Fact]
        public async Task GenerateResponseAsync_FallsBackToRemote_WhenLocalReturnsNonJson()
        {
            var localHandler = new StubHttpMessageHandler(_ => BuildChatResponse("local-model", "not-json"));
            var remoteHandler = new StubHttpMessageHandler(_ => BuildChatResponse(
                "remote-model",
                """{"replyText":"Stay with the facts.","allowedTopicsUsed":["public_story"]}"""));

            var service = new LlmService(
                new StubHttpClientFactory(new Dictionary<string, HttpClient>
                {
                    ["llm-local"] = new HttpClient(localHandler),
                    ["llm-remote"] = new HttpClient(remoteHandler)
                }),
                new StaticOptionsMonitor<LlmOptions>(new LlmOptions
                {
                    Local = new LocalLlmOptions
                    {
                        Enabled = true,
                        BaseUrl = "http://localhost:11434",
                        Model = "local-model"
                    },
                    Remote = new RemoteLlmOptions
                    {
                        Enabled = true,
                        BaseUrl = "https://example.com/v1",
                        Model = "remote-model",
                        ApiKey = "test-key"
                    }
                }),
                NullLogger<LlmService>.Instance);

            var response = await service.GenerateResponseAsync(new LlmPromptPayload
            {
                promptText = "context",
                requireJson = true
            });

            Assert.Equal("remote", response.provider);
            Assert.True(response.usedFallback);
            Assert.Equal("remote-model", response.modelName);
            Assert.Equal(1, localHandler.CallCount);
            Assert.Equal(1, remoteHandler.CallCount);
        }

        [Fact]
        public async Task GenerateResponseAsync_UsesLocal_WhenLocalReturnsValidJson()
        {
            var localHandler = new StubHttpMessageHandler(_ => BuildChatResponse(
                "local-model",
                """{"replyText":"I already answered that.","allowedTopicsUsed":["public_story"]}"""));

            var service = new LlmService(
                new StubHttpClientFactory(new Dictionary<string, HttpClient>
                {
                    ["llm-local"] = new HttpClient(localHandler),
                    ["llm-remote"] = new HttpClient(new StubHttpMessageHandler(_ => throw new Xunit.Sdk.XunitException("Remote should not be called.")))
                }),
                new StaticOptionsMonitor<LlmOptions>(new LlmOptions
                {
                    Local = new LocalLlmOptions
                    {
                        Enabled = true,
                        BaseUrl = "http://localhost:11434",
                        Model = "local-model"
                    },
                    Remote = new RemoteLlmOptions
                    {
                        Enabled = true,
                        BaseUrl = "https://example.com/v1",
                        Model = "remote-model",
                        ApiKey = "test-key"
                    }
                }),
                NullLogger<LlmService>.Instance);

            var response = await service.GenerateResponseAsync(new LlmPromptPayload
            {
                promptText = "context",
                requireJson = true
            });

            Assert.Equal("local", response.provider);
            Assert.False(response.usedFallback);
            Assert.Equal("local-model", response.modelName);
            Assert.Equal(1, localHandler.CallCount);
        }

        [Fact]
        public async Task GenerateResponseAsync_SendsSeed_ForLocalProvider_WhenConfigured()
        {
            var localHandler = new StubHttpMessageHandler(_ => BuildChatResponse(
                "local-model",
                """{"replyText":"Keep it simple.","allowedTopicsUsed":["public_story"]}"""));

            var service = new LlmService(
                new StubHttpClientFactory(new Dictionary<string, HttpClient>
                {
                    ["llm-local"] = new HttpClient(localHandler),
                    ["llm-remote"] = new HttpClient(new StubHttpMessageHandler(_ => throw new Xunit.Sdk.XunitException("Remote should not be called.")))
                }),
                new StaticOptionsMonitor<LlmOptions>(new LlmOptions
                {
                    Local = new LocalLlmOptions
                    {
                        Enabled = true,
                        BaseUrl = "http://localhost:11434",
                        Model = "qwen2.5:3b",
                        Seed = 17
                    }
                }),
                NullLogger<LlmService>.Instance);

            await service.GenerateResponseAsync(new LlmPromptPayload
            {
                promptText = "context",
                requireJson = true
            });

            Assert.NotNull(localHandler.LastRequestBody);
            using var document = JsonDocument.Parse(localHandler.LastRequestBody!);
            Assert.True(document.RootElement.TryGetProperty("seed", out var seedElement));
            Assert.Equal(17, seedElement.GetInt32());
        }

        private static HttpResponseMessage BuildChatResponse(string model, string messageContent)
        {
            var payload = $$"""
{
  "model": "{{model}}",
  "choices": [
    {
      "message": {
        "content": {{System.Text.Json.JsonSerializer.Serialize(messageContent)}}
      },
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "total_tokens": 42
  }
}
""";

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
        }

        private sealed class StubHttpClientFactory : IHttpClientFactory
        {
            private readonly IReadOnlyDictionary<string, HttpClient> _clients;

            public StubHttpClientFactory(IReadOnlyDictionary<string, HttpClient> clients)
            {
                _clients = clients;
            }

            public HttpClient CreateClient(string name)
            {
                return _clients[name];
            }
        }

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
            {
                _handler = handler;
            }

            public int CallCount { get; private set; }
            public string? LastRequestBody { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                CallCount++;
                LastRequestBody = request.Content is null
                    ? null
                    : request.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
                return Task.FromResult(_handler(request));
            }
        }
    }
}
