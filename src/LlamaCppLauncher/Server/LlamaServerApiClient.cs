using System.Net.Http;
using System.Text.Json;

namespace LlamaCppLauncher.Server;

public sealed class LlamaServerApiClient
{
    private readonly HttpClient _httpClient;

    public LlamaServerApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RunningModelInfo?> GetRunningModelInfoAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        string url = $"http://{host}:{port}/v1/models";

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        ModelsApiResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<ModelsApiResponse>(json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (parsed is null || parsed.Data.Count == 0)
        {
            return null;
        }

        ModelsApiEntry entry = parsed.Data[0];
        return new RunningModelInfo(
            Alias: entry.Id,
            ContextSize: entry.Meta?.NCtx ?? 0,
            Quantization: entry.Meta?.Ftype ?? "Unknown",
            TotalParams: entry.Meta is null ? "Unknown" : ParamFormatter.FormatTotalParams(entry.Meta.NParams));
    }
}
