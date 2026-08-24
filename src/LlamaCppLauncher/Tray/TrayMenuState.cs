namespace LlamaCppLauncher.Tray;

public sealed class SwitchMenuEntry
{
    public required string Alias { get; init; }
    public required string FileName { get; init; }
    public bool IsCurrent { get; init; }
}

public sealed class TrayMenuState
{
    public ServiceState ServiceState { get; init; }
    public bool SwitchEnabled { get; init; }
    public IReadOnlyList<SwitchMenuEntry> SwitchEntries { get; init; } = Array.Empty<SwitchMenuEntry>();
}
