// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;
using Duende.ConformanceReport.Internal.Models;

namespace Duende.ConformanceReport.Services;

[StackTraceHidden]
[ShouldlyMethods]
internal static class FindingAssertions
{
    /// <summary>
    /// Asserts that every finding has one of the allowed statuses. On failure, lists each
    /// offending finding by rule ID, status, and message so the failing rule is obvious.
    /// </summary>
    public static void ShouldAllHaveStatus(this IEnumerable<Finding> findings, params FindingStatus[] allowedStatuses)
    {
        var unexpectedFindings = findings
            .Where(f => !allowedStatuses.Contains(f.Status))
            .Select(f => $"{f.RuleId}: {f.Status} - {f.Message}")
            .ToList();

        if (unexpectedFindings.Count > 0)
        {
            // Throwing an exception in a class decorated with ShouldlyMethods and StackTraceHidden instead of calling a shouldly assertion
            // makes the IDE experience better when a test that uses this assertion fails. Navigation goes to the failing test, rather than
            // here.
            throw new FindingAssertionException(
                $"Expected all findings to be {string.Join(" or ", allowedStatuses)}, but found:{Environment.NewLine}" +
                string.Join(Environment.NewLine, unexpectedFindings));
        }
    }

    public sealed class FindingAssertionException : Exception
    {
        public FindingAssertionException(string? message) : base(message)
        {
        }
    }

}
