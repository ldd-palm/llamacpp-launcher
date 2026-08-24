using Microsoft.Win32;

namespace LlamaCppLauncher.AutoStart;

public sealed class RegistryRunKeyStore : IRunKeyStore
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public void SetValue(string name, string value)
    {
        using RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(name, value);
    }

    public void RemoveValue(string name)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }

    public bool TryGetValue(string name, out string? value)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        value = key?.GetValue(name) as string;
        return value is not null;
    }
}
