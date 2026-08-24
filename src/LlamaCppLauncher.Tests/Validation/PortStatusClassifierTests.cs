using LlamaCppLauncher.Validation;

namespace LlamaCppLauncher.Tests.Validation;

public class PortStatusClassifierTests
{
    [Fact]
    public void Classify_ReturnsFree_WhenNotListening()
    {
        Assert.Equal(PortStatus.Free, PortStatusClassifier.Classify(isListening: false, isOwnedByLauncherProcess: false));
    }

    [Fact]
    public void Classify_ReturnsOccupiedByLauncher_WhenListeningAndOwnedByLauncher()
    {
        Assert.Equal(PortStatus.OccupiedByLauncher, PortStatusClassifier.Classify(isListening: true, isOwnedByLauncherProcess: true));
    }

    [Fact]
    public void Classify_ReturnsOccupiedByOther_WhenListeningAndNotOwnedByLauncher()
    {
        Assert.Equal(PortStatus.OccupiedByOther, PortStatusClassifier.Classify(isListening: true, isOwnedByLauncherProcess: false));
    }
}
