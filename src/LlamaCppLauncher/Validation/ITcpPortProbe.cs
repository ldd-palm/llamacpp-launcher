namespace LlamaCppLauncher.Validation;

public interface ITcpPortProbe
{
    bool IsPortListening(int port);
}
