namespace LlamaCppLauncher.Tray;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;

    public bool IsFirstInstance { get; }

    public SingleInstanceService(string mutexName)
    {
        _mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out bool createdNew);
        IsFirstInstance = createdNew;
    }

    public void Dispose()
    {
        if (IsFirstInstance)
        {
            _mutex.ReleaseMutex();
        }
        _mutex.Dispose();
    }
}
