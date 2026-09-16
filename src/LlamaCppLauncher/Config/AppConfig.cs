namespace LlamaCppLauncher.Config;

public sealed class AppConfig
{
    public string ExecutablePath { get; set; } = string.Empty;
    public string ModelsDirectory { get; set; } = string.Empty;
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8080;
    public bool StartWithWindows { get; set; }
    public string? LastRunningModelFileName { get; set; }

    /// "Light" or "Dark" — applied to the Settings/About windows via WPF-UI's ApplicationThemeManager.
    public string Theme { get; set; } = "Light";
    public List<ModelProfile> Models { get; set; } = new();
}
