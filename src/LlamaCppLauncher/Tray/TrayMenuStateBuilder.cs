using LlamaCppLauncher.Config;

namespace LlamaCppLauncher.Tray;

public static class TrayMenuStateBuilder
{
    public static TrayMenuState Build(ServiceState state, IReadOnlyList<ModelProfile> models, string? runningFileName)
    {
        List<SwitchMenuEntry> entries = models
            .OrderBy(m => m.Alias, StringComparer.OrdinalIgnoreCase)
            .Select(m => new SwitchMenuEntry
            {
                Alias = m.Alias,
                FileName = m.FileName,
                IsCurrent = state == ServiceState.On &&
                    string.Equals(m.FileName, runningFileName, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();

        return new TrayMenuState
        {
            ServiceState = state,
            SwitchEnabled = state == ServiceState.On,
            SwitchEntries = entries
        };
    }
}
