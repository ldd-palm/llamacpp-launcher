using LlamaCppLauncher.About;
using LlamaCppLauncher.Server;

namespace LlamaCppLauncher.Tests.About;

public class AboutViewModelTests
{
    [Fact]
    public void NotRunning_SetsIsServiceRunningFalse()
    {
        AboutViewModel viewModel = AboutViewModel.NotRunning();

        Assert.False(viewModel.IsServiceRunning);
    }

    [Fact]
    public void FromRunningModel_PopulatesAllFieldsFromModelInfo()
    {
        var info = new RunningModelInfo("Qwen2.5-7B", 8192, "Q4_K_M", "7.62 B");

        AboutViewModel viewModel = AboutViewModel.FromRunningModel("Qwen2.5-7B-Instruct-Q4_K_M.gguf", "127.0.0.1", 8080, info);

        Assert.True(viewModel.IsServiceRunning);
        Assert.Equal("Qwen2.5-7B-Instruct-Q4_K_M.gguf", viewModel.FileName);
        Assert.Equal("Qwen2.5-7B", viewModel.Alias);
        Assert.Equal("http://127.0.0.1:8080/", viewModel.WebChatUrl);
        Assert.Equal("http://127.0.0.1:8080/v1", viewModel.ApiUrl);
        Assert.Equal("Q4_K_M", viewModel.Quantization);
        Assert.Equal("7.62 B", viewModel.TotalParams);
        Assert.Equal("8192", viewModel.ContextSize);
    }

    [Fact]
    public void ProjectHomepageUrl_PointsToLlamaCppRepository()
    {
        AboutViewModel viewModel = AboutViewModel.NotRunning();

        Assert.Equal("https://github.com/ggml-org/llama.cpp", viewModel.ProjectHomepageUrl);
    }
}
