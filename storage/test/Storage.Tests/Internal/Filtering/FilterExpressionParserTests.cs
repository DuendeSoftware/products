// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Querying;

namespace Duende.Storage.Internal.Filtering;

public sealed class FilterExpressionParserTests
{
    [Fact]
    public void filter_at_max_conditions_limit_succeeds()
    {
        // 30 conditions chained with "and" — should parse successfully
        var conditions = Enumerable.Range(1, 30)
            .Select(i => $"field{i} eq \"value{i}\"");
        var filter = string.Join(" and ", conditions);

        var result = FilterExpressionParser.Parse(filter);

        _ = result.ShouldNotBeNull();
    }

    [Fact]
    public void complex_filter_at_max_conditions_limit_succeeds()
    {
        // 28 top-level + emails entry + 1 inner condition = exactly 30
        var topConditions = Enumerable.Range(1, 28)
            .Select(i => $"field{i} eq \"value{i}\"");
        var filter = string.Join(" and ", topConditions) + " and emails[type eq \"work\"]";

        var result = FilterExpressionParser.Parse(filter);

        _ = result.ShouldNotBeNull();
    }

    [Fact]
    public void filter_exceeding_max_conditions_throws()
    {
        // 31 conditions — should exceed the limit
        var conditions = Enumerable.Range(1, 31)
            .Select(i => $"field{i} eq \"value{i}\"");
        var filter = string.Join(" and ", conditions);

        var ex = Should.Throw<FilterParseException>(() => FilterExpressionParser.Parse(filter));

        ex.Message.ShouldBe("Filter expression exceeds maximum of 30 conditions at position 732");
    }

    [Fact]
    public void conditions_inside_complex_filters_count_toward_limit()
    {
        // 29 top-level + 1 for emails entry + 1 for first inner condition = 31 > 30
        // (second inner condition is never reached because the limit fires first)
        var topConditions = Enumerable.Range(1, 29)
            .Select(i => $"field{i} eq \"value{i}\"");
        var filter = string.Join(" and ", topConditions) + " and emails[type eq \"work\" and value eq \"x\"]";

        var ex = Should.Throw<FilterParseException>(() => FilterExpressionParser.Parse(filter));

        ex.Message.ShouldBe("Filter expression exceeds maximum of 30 conditions at position 714");
    }

    [Fact]
    public void or_conditions_count_toward_limit()
    {
        // 31 conditions chained with "or" — should also exceed the limit
        var conditions = Enumerable.Range(1, 31)
            .Select(i => $"field{i} eq \"value{i}\"");
        var filter = string.Join(" or ", conditions);

        var ex = Should.Throw<FilterParseException>(() => FilterExpressionParser.Parse(filter));

        ex.Message.ShouldBe("Filter expression exceeds maximum of 30 conditions at position 702");
    }

    [Fact]
    public void mixed_and_or_conditions_count_toward_limit()
    {
        // 16 "and" conditions or'd with 16 more = 32 total conditions
        var group1 = string.Join(" and ", Enumerable.Range(1, 16).Select(i => $"a{i} eq \"v\""));
        var group2 = string.Join(" and ", Enumerable.Range(1, 16).Select(i => $"b{i} eq \"v\""));
        var filter = $"({group1}) or ({group2})";

        var ex = Should.Throw<FilterParseException>(() => FilterExpressionParser.Parse(filter));

        ex.Message.ShouldBe("Filter expression exceeds maximum of 30 conditions at position 434");
    }
}
