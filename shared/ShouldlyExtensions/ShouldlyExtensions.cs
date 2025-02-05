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

        if (missingItems.Any())
        {
            throw new ShouldAssertException(
                $"Expected collection to contain all items, but these were missing: {string.Join(", ", missingItems)}.\n" +
                $"Actual: [{string.Join(", ", actual)}]\n" +
                $"Expected: [{string.Join(", ", expected)}]"
            );
        }
    }
}