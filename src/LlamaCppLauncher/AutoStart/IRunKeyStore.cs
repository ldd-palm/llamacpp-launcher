namespace LlamaCppLauncher.AutoStart;

public interface IRunKeyStore
{
    void SetValue(string name, string value);
    void RemoveValue(string name);
    bool TryGetValue(string name, out string? value);
}
