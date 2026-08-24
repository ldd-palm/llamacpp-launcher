using LlamaCppLauncher.Config;
using LlamaCppLauncher.Tray;

namespace LlamaCppLauncher.Tests.Tray;

public class TrayMenuStateBuilderTests
{
    private static ModelProfile Model(string fileName, string alias) => new() { FileName = fileName, Alias = alias };

    [Fact]
    public void Build_DisablesSwitch_WhenServiceOff()
    {
        var models = new List<ModelProfile> { Model("a.gguf", "A") };

        TrayMenuState state = TrayMenuStateBuilder.Build(ServiceState.Off, models, runningFileName: null);

        Assert.False(state.SwitchEnabled);
    }

    [Fact]
    public void Build_EnablesSwitch_AndMarksRunningModelAsCurrent_WhenServiceOn()
    {
        var models = new List<ModelProfile> { Model("a.gguf", "Alpha"), Model("b.gguf", "Beta") };

        TrayMenuState state = TrayMenuStateBuilder.Build(ServiceState.On, models, runningFileName: "b.gguf");

        Assert.True(state.SwitchEnabled);
        Assert.Equal(2, state.SwitchEntries.Count);
        SwitchMenuEntry currentEntry = Assert.Single(state.SwitchEntries, e => e.IsCurrent);
        Assert.Equal("b.gguf", currentEntry.FileName);
    }

    [Fact]
    public void Build_SortsEntriesByAliasCaseInsensitive()
    {
        var models = new List<ModelProfile> { Model("z.gguf", "zeta"), Model("a.gguf", "Alpha") };

        TrayMenuState state = TrayMenuStateBuilder.Build(ServiceState.On, models, runningFileName: null);

        Assert.Equal("Alpha", state.SwitchEntries[0].Alias);
        Assert.Equal("zeta", state.SwitchEntries[1].Alias);
    }

    [Fact]
    public void Build_NoEntryIsCurrent_WhenServiceOff_EvenIfRunningFileNameMatches()
    {
        var models = new List<ModelProfile> { Model("a.gguf", "A") };

        TrayMenuState state = TrayMenuStateBuilder.Build(ServiceState.Off, models, runningFileName: "a.gguf");

        Assert.DoesNotContain(state.SwitchEntries, e => e.IsCurrent);
    }
}
