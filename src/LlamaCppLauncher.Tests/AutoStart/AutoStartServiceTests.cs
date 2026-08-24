using LlamaCppLauncher.AutoStart;

namespace LlamaCppLauncher.Tests.AutoStart;

public class AutoStartServiceTests
{
    private sealed class InMemoryRunKeyStore : IRunKeyStore
    {
        private readonly Dictionary<string, string> _values = new();

        public void SetValue(string name, string value) => _values[name] = value;
        public void RemoveValue(string name) => _values.Remove(name);
        public bool TryGetValue(string name, out string? value) => _values.TryGetValue(name, out value);
    }

    [Fact]
    public void IsEnabled_ReturnsFalse_WhenValueNotSet()
    {
        var service = new AutoStartService(new InMemoryRunKeyStore());

        Assert.False(service.IsEnabled());
    }

    [Fact]
    public void SetEnabled_True_WritesQuotedExecutablePath()
    {
        var store = new InMemoryRunKeyStore();
        var service = new AutoStartService(store);

        service.SetEnabled(true, @"C:\LlamaCppLauncher\LlamaCppLauncher.exe");

        Assert.True(service.IsEnabled());
        store.TryGetValue("LlamaCppLauncher", out string? value);
        Assert.Equal("\"C:\\LlamaCppLauncher\\LlamaCppLauncher.exe\"", value);
    }

    [Fact]
    public void SetEnabled_False_RemovesValue()
    {
        var store = new InMemoryRunKeyStore();
        var service = new AutoStartService(store);
        service.SetEnabled(true, @"C:\app.exe");

        service.SetEnabled(false, @"C:\app.exe");

        Assert.False(service.IsEnabled());
    }
}
