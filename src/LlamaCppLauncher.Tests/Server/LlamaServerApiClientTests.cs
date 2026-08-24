using System.Net;
using LlamaCppLauncher.Server;

namespace LlamaCppLauncher.Tests.Server;

public class LlamaServerApiClientTests
{
    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public StubHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode) { Content = new StringContent(_content) };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task GetRunningModelInfoAsync_ParsesModelDetails()
    {
        const string json = """
        {
          "data": [
            { "id": "Qwen2.5-7B", "meta": { "n_ctx": 8192, "ftype": "Q4_K_M", "n_params": 7620000000 } }
          ]
        }
        """;
        var client = new LlamaServerApiClient(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, json)));

        RunningModelInfo? info = await client.GetRunningModelInfoAsync("127.0.0.1", 8080);

        Assert.NotNull(info);
        Assert.Equal("Qwen2.5-7B", info!.Alias);
        Assert.Equal(8192, info.ContextSize);
        Assert.Equal("Q4_K_M", info.Quantization);
        Assert.Equal("7.62 B", info.TotalParams);
    }

    [Fact]
    public async Task GetRunningModelInfoAsync_ReturnsNull_WhenServerRespondsWithError()
    {
        var client = new LlamaServerApiClient(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.InternalServerError, "")));

        RunningModelInfo? info = await client.GetRunningModelInfoAsync("127.0.0.1", 8080);

        Assert.Null(info);
    }

    [Fact]
    public async Task GetRunningModelInfoAsync_ReturnsNull_WhenResponseHasNoModels()
    {
        var client = new LlamaServerApiClient(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.OK, """{ "data": [] }""")));

        RunningModelInfo? info = await client.GetRunningModelInfoAsync("127.0.0.1", 8080);

        Assert.Null(info);
    }
}
