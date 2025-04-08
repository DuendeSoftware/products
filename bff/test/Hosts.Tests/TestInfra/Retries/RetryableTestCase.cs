// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Duende.Hosts.Tests.TestInfra.Retries;

public class RetryableTestCase(
    IMessageSink sink,
    TestMethodDisplay display,
    TestMethodDisplayOptions methodDisplayOptions,
    ITestMethod method
) : XunitTestCase(sink,
    display,
    methodDisplayOptions,
    method,
    testMethodArguments: null)
{
    public override async Task<RunSummary> RunAsync(
        IMessageSink diagnosticMessageSink,
        IMessageBus messageBus,
        object[] constructorArguments,
        ExceptionAggregator aggregator,
        CancellationTokenSource cts)
    {
        var retryCount = 0;
        var maxRetries = Method.GetCustomAttributes(typeof(RetryableFact)).FirstOrDefault()?.GetNamedArgument<int>(nameof(RetryableFact.MaxRetries)) ?? 5;

        while (true)
        {
            retryCount++;

            var exceptionCapturingBus = new ExceptionCapturingMessageBus(messageBus);
            var summary = await base.RunAsync(
                diagnosticMessageSink,
                exceptionCapturingBus,
                constructorArguments,
                aggregator,
                cts);

            summary.Failed -= exceptionCapturingBus.SkippedCount;
            summary.Skipped += exceptionCapturingBus.SkippedCount;

            if (aggregator.HasExceptions || summary.Failed == 0 || retryCount >= maxRetries)
            {
                exceptionCapturingBus.Flush();
                return summary;
            }

            diagnosticMessageSink.OnMessage(new DiagnosticMessage(
                "Execution of retriable test '{0}' failed. Attempt {1} of {2}",
                DisplayName,
                retryCount,
                maxRetries
            ));
        }
    }


}
