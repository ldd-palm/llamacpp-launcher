namespace LlamaCppLauncher.Validation;

public static class PortStatusClassifier
{
    public static PortStatus Classify(bool isListening, bool isOwnedByLauncherProcess)
    {
        if (!isListening)
        {
            return PortStatus.Free;
        }

        return isOwnedByLauncherProcess ? PortStatus.OccupiedByLauncher : PortStatus.OccupiedByOther;
    }
}
