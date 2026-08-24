namespace LlamaCppLauncher.Validation;

public interface IPortChecker
{
    PortStatus GetStatus(int port);
}
