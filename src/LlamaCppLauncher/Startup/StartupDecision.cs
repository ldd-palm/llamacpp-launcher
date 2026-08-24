using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Startup;

public sealed class StartupDecision
{
    public required StartupAction Action { get; init; }
    public string? Message { get; init; }
    public AppConfig? Config { get; init; }
    public ModelProfile? DefaultModel { get; init; }
}
