// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Xunit.v3;

namespace Duende.IdentityServer.IntegrationTests.Common;

/// <summary>
/// Marks a store contract test that never reaches storage, for example because the store rejects its
/// input before any storage call. The Storage contract tests then check that no storage category was
/// used, instead of requiring the expected one.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class DoesNotTouchStorageAttribute : Attribute
{
    /// <summary>
    /// Returns whether the currently running test method carries this attribute.
    /// </summary>
    /// <returns><c>true</c> when the running test is marked; otherwise <c>false</c>.</returns>
    public static bool IsSetOnCurrentTest() =>
        TestContext.Current.TestMethod is IXunitTestMethod testMethod &&
        testMethod.Method.IsDefined(typeof(DoesNotTouchStorageAttribute), inherit: true);
}
