namespace LlamaCppLauncher.AutoStart;

public sealed class AutoStartService
{
    private const string ValueName = "LlamaCppLauncher";
    private readonly IRunKeyStore _store;

    public AutoStartService(IRunKeyStore store)
    {
        _store = store;
    }

    public bool IsEnabled() => _store.TryGetValue(ValueName, out _);

    public void SetEnabled(bool enabled, string executablePath)
    {
        if (enabled)
        {
            _store.SetValue(ValueName, $"\"{executablePath}\"");
        }
        else
        {
            _store.RemoveValue(ValueName);
        }
    }
}
