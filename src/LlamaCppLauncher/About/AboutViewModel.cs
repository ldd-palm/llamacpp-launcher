using CommunityToolkit.Mvvm.ComponentModel;
using LlamaCppLauncher.Server;

namespace LlamaCppLauncher.About;

public sealed partial class AboutViewModel : ObservableObject
{
    private const string ProjectUrl = "https://github.com/ggml-org/llama.cpp";

    [ObservableProperty]
    private bool _isServiceRunning;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _alias = string.Empty;

    [ObservableProperty]
    private string _webChatUrl = string.Empty;

    [ObservableProperty]
    private string _apiUrl = string.Empty;

    [ObservableProperty]
    private string _quantization = string.Empty;

    [ObservableProperty]
    private string _totalParams = string.Empty;

    [ObservableProperty]
    private string _contextSize = string.Empty;

    public string ProjectHomepageUrl => ProjectUrl;

    public static AboutViewModel NotRunning() => new() { IsServiceRunning = false };

    public static AboutViewModel FromRunningModel(string fileName, string host, int port, RunningModelInfo info)
    {
        return new AboutViewModel
        {
            IsServiceRunning = true,
            FileName = fileName,
            Alias = info.Alias,
            WebChatUrl = $"http://{host}:{port}/",
            ApiUrl = $"http://{host}:{port}/v1",
            Quantization = info.Quantization,
            TotalParams = info.TotalParams,
            ContextSize = info.ContextSize.ToString()
        };
    }
}
