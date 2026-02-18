// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel;
using Xunit.Sdk;
using Xunit.v3;

namespace Duende.Xunit.Playwright.Retries;

// This class is used for retriable facts.
internal class RetryableTestCase : XunitTestCase, ISelfExecutingXunitTestCase
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
    public RetryableTestCase() { }

    public RetryableTestCase(
        int maxRetries,
        IXunitTestMethod testMethod,
        string testCaseDisplayName,
        string uniqueID,
        bool @explicit,
        Type[]? skipExceptions = null,
        string? skipReason = null,
        Type? skipType = null,
        string? skipUnless = null,
        string? skipWhen = null,
        Dictionary<string, HashSet<string>>? traits = null,
        object?[]? testMethodArguments = null,
        string? sourceFilePath = null,
        int? sourceLineNumber = null,
        int? timeout = null) :
            base(testMethod, testCaseDisplayName, uniqueID, @explicit, skipExceptions, skipReason, skipType, skipUnless, skipWhen, traits, testMethodArguments, sourceFilePath, sourceLineNumber, timeout)
    {
        MaxRetries = maxRetries;
    }

    public int MaxRetries { get; private set; }

    protected override void Deserialize(IXunitSerializationInfo info)
    {
        base.Deserialize(info);
        MaxRetries = info.GetValue<int>(nameof(MaxRetries));
    }

    public ValueTask<RunSummary> Run(
        ExplicitOption explicitOption,
        IMessageBus messageBus,
        object?[] constructorArguments,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource) =>
            RetryTestCaseRunner.Instance.Run(
                MaxRetries,
                this,
                messageBus,
                aggregator.Clone(),
                cancellationTokenSource,
                TestCaseDisplayName,
                SkipReason,
                explicitOption,
                constructorArguments
            );

    protected override void Serialize(IXunitSerializationInfo info)
    {
        base.Serialize(info);
        info.AddValue(nameof(MaxRetries), MaxRetries);
    }
}
