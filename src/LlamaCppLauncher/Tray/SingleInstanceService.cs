namespace LlamaCppLauncher.Tray;

public sealed class SingleInstanceService : IDisposable
{
    private readonly Mutex _mutex;
    private bool _disposed;

    public bool IsFirstInstance { get; }

    // Windows names kernel objects like this Mutex per-process-lifetime: if the first
    // instance crashes, the OS closes its handle to the named mutex as part of process
    // teardown, which destroys the object outright (this class never re-waits on it, so
    // the classic AbandonedMutexException path never applies here). The next launch's
    // constructor call then correctly observes createdNew == true and takes over.
    public SingleInstanceService(string mutexName)
    {
        _mutex = new Mutex(initiallyOwned: true, name: mutexName, createdNew: out bool createdNew);
        IsFirstInstance = createdNew;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (IsFirstInstance)
        {
            _mutex.ReleaseMutex();
        }
        _mutex.Dispose();
    }
}
