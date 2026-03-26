using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using testapi1.Application;
using testapi1.Infrastructure.VectorStores.Qdrant;
using testapi1.Services.Connectivity;
using testapi1.Services.Llm;
using testapi1.Tests.TestSupport;

namespace testapi1.Tests
{
    public sealed class RemoteDependencyProbeServiceTests
    {
        [Fact]
        public async Task ProbeAsync_IncludesOllamaStatus_WithConfiguredModelMetadata()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Redis"] = "127.0.0.1:1",
                    ["ConnectionStrings:Postgres"] = "Host=127.0.0.1;Port=1;Database=testgame;Username=postgres;Password=secret;Timeout=1;Command Timeout=1"
                })
                .Build();

            var probeService = new RemoteDependencyProbeService(
                configuration,
                new StaticOptionsMonitor<RemoteConnectivityOptions>(new RemoteConnectivityOptions
                {
                    TimeoutMs = 250
                }),
                new StaticOptionsMonitor<QdrantOptions>(new QdrantOptions
                {
                    BaseUrl = "http://127.0.0.1:6333",
                    CollectionName = "intent-seed-poc"
                }),
                new StaticOptionsMonitor<LlmOptions>(new LlmOptions
                {
                    Local = new LocalLlmOptions
                    {
                        Enabled = true,
                        BaseUrl = "http://localhost:11434",
                        Model = "qwen2.5:3b"
                    }
                }),
                new StubHttpClientFactory(new Dictionary<string, HttpClient>
                {
                    ["remote-dependency-probe"] = new HttpClient(new StubHttpMessageHandler(_ =>
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{}", Encoding.UTF8, "application/json")
                        })),
                    ["llm-local"] = new HttpClient(new StubHttpMessageHandler(_ =>
                        new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent(
                                """{"data":[{"id":"qwen2.5:3b"},{"id":"other-model"}]}""",
                                Encoding.UTF8,
                                "application/json")
                        }))
                }));

            var report = await probeService.ProbeAsync();

            var ollama = Assert.Single(report.Dependencies, item => item.Name == "ollama");
            Assert.True(ollama.Healthy);
            Assert.Equal("qwen2.5:3b", ollama.ConfiguredModel);
            Assert.True(ollama.ModelAvailable);
            Assert.Equal("Configured model found.", ollama.Message);
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

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_handler(request));
            }
        }
    }
}
