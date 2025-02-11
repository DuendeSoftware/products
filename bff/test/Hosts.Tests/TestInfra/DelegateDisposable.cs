namespace Hosts.Tests.TestInfra;

public class DelegateDisposable(Action onDispose) : IDisposable
{
    public void Dispose()
    {
        onDispose();
    }
}