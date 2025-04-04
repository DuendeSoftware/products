using Xunit.Abstractions;
using Xunit.Sdk;

namespace Duende.Hosts.Tests.TestInfra.Retries;

public class RetryableTestDiscoverer : IXunitTestCaseDiscoverer
{
    private readonly IMessageSink _messageSink;

    public RetryableTestDiscoverer(IMessageSink messageSink)
    {
        _messageSink = messageSink;
    }

    public IEnumerable<IXunitTestCase> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions, 
        ITestMethod testMethod, 
        IAttributeInfo factAttribute)
    {
        yield return new RetryableTestCase(
            _messageSink,
            discoveryOptions.MethodDisplayOrDefault(),
            discoveryOptions.MethodDisplayOptionsOrDefault(),
            testMethod
        );
    }

    public class ExceptionCapturingMessageBus(IMessageBus inner) : IMessageBus
    {
        private readonly object _syncRoot = new();
        private readonly Queue<IMessageSinkMessage> _failedMessages = new();
        private bool _disposed = false;

        public bool ExceptionHasOccurred { get; private set; }

        public bool QueueMessage(IMessageSinkMessage message)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ExceptionCapturingMessageBus));

            if (message is ITestFailed failed)
            {
                // We ignore 'skip' exceptions
                if (!failed.ExceptionTypes.Contains("XUnit.SkipException"))
                {
                    ExceptionHasOccurred = true;
                }
            }

            lock (_syncRoot)
            {
                _failedMessages.Enqueue(message);
            }

            return true;
        }

        public void Flush()
        {
            lock (_syncRoot)
            {
                while (_failedMessages.Any())
                {
                    inner.QueueMessage(_failedMessages.Dequeue());
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_syncRoot)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
            }
            
            Flush();
        }
    }
}
