using LlamaCppLauncher.Validation;

namespace LlamaCppLauncher.Tests.TestSupport;

public sealed class StubPortChecker : IPortChecker
{
    private readonly PortStatus _status;

    public StubPortChecker(PortStatus status)
    {
        _status = status;
    }

    public PortStatus GetStatus(int port) => _status;
}
