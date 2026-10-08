// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Xunit.Internal;
using Xunit.Sdk;
using Xunit.v3;

namespace Duende.Xunit.Playwright.Retries;

internal class RetryFactDiscoverer : IXunitTestCaseDiscoverer
{
    public ValueTask<IReadOnlyCollection<IXunitTestCase>> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        IXunitTestMethod testMethod,
        IFactAttribute factAttribute)
    {
        var maxRetries = (factAttribute as RetryableFactAttribute)?.MaxRetries ?? 5;
        var details = TestIntrospectionHelper.GetTestCaseDetails(discoveryOptions, testMethod, factAttribute);
        var testCase = new RetryableTestCase(
            maxRetries,
            details.ResolvedTestMethod,
            details.TestCaseDisplayName,
            details.UniqueID,
            details.Explicit,
            details.SkipExceptions,
            details.SkipReason,
            details.SkipType,
            details.SkipUnless,
            details.SkipWhen,
            testMethod.Traits.ToReadWrite(StringComparer.OrdinalIgnoreCase),
            timeout: details.Timeout
        );

        return ValueTask.FromResult<IReadOnlyCollection<IXunitTestCase>>([testCase]);
    }
}
