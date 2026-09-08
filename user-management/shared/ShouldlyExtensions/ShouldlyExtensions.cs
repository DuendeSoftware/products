// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Shouldly;

public static class ShouldlyExtensions
{
    /// <summary>
    /// Asserts that the actual DateTime is within the specified TimeSpan of the expected DateTime.
    /// </summary>
    /// <param name="actual">The actual DateTime value.</param>
    /// <param name="expected">The expected DateTime value.</param>
    /// <param name="tolerance">The allowed TimeSpan difference.</param>
    /// <param name="customMessage">A custom error message if the assertion fails.</param>
    public static void ShouldBeCloseTo(this DateTime actual, DateTime expected, TimeSpan tolerance, string? customMessage = null)
    {
        var difference = (actual - expected).Duration();

        if (difference <= tolerance)
        {
            return;
        }
        var errorMessage = customMessage ??
                           $"Expected {actual} to be within {tolerance} of {expected}, but the difference was {difference}.";
        throw new ShouldAssertException(errorMessage);
    }

    /// <summary>
    /// Asserts that the actual DateTime is within the specified TimeSpan of the expected DateTime.
    /// </summary>
    /// <param name="actual">The actual DateTime value.</param>
    /// <param name="expected">The expected DateTime value.</param>
    /// <param name="tolerance">The allowed TimeSpan difference.</param>
    /// <param name="customMessage">A custom error message if the assertion fails.</param>
    public static void ShouldBeCloseTo(this DateTimeOffset actual, DateTimeOffset expected, TimeSpan tolerance, string? customMessage = null)
    {
        var difference = (actual - expected).Duration();

        if (difference <= tolerance)
        {
            return;
        }

        var errorMessage = customMessage ??
                           $"Expected {actual} to be within {tolerance} of {expected}, but the difference was {difference}.";
        throw new ShouldAssertException(errorMessage);
    }

    /// <summary>
    /// Asserts that each item in the expected collection is contained in the actual collection.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="actual"></param>
    /// <param name="expected"></param>
    /// <exception cref="ShouldAssertException"></exception>
    public static void ShouldContain<T>(this IEnumerable<T> actual, IEnumerable<T> expected)
    {
        var missingItems = expected.Where(item => !actual.Contains(item)).ToList();

        if (missingItems.Count > 0)
        {
            throw new ShouldAssertException(
                $"Expected collection to contain all items, but these were missing: {string.Join(", ", missingItems)}.\n" +
                $"Actual: [{string.Join(", ", actual)}]\n" +
                $"Expected: [{string.Join(", ", expected)}]"
            );
        }
    }

    /// <summary>
    /// Polls the specified assertion until it passes or the timeout is exceeded.
    /// Useful for asserting eventual consistency when background services are involved.
    /// </summary>
    /// <param name="action">The assertion to evaluate repeatedly.</param>
    /// <param name="timeout">Maximum time to wait. Defaults to 5 seconds.</param>
    /// <param name="pollInterval">Delay between attempts. Defaults to 100ms.</param>
    public static async Task ShouldSatisfyEventually(Func<Task> action, TimeSpan? timeout = null, TimeSpan? pollInterval = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        var interval = pollInterval ?? TimeSpan.FromMilliseconds(100);

        while (true)
        {
            try
            {
                await action();
                return;
            }
            catch when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(interval);
            }
        }
    }
}
