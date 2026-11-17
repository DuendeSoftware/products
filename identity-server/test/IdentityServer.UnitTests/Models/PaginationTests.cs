// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using Models = Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.ModelsTests;

/// <summary>
/// Tests construction validation, implicit conversions, and exception shapes for the paging value
/// objects in <see cref="Duende.IdentityServer.Models"/>, which recreate the API surface that
/// Duende.Storage exposed before the merge. Behaviour that matters: constructing from an invalid
/// value fails immediately, with the same exception type and message Storage used.
/// </summary>
public class PaginationTests
{
    // === PageNumber ===

    [Fact]
    public void PageNumber_zero_throws_FormatException_with_the_same_message_as_Storage()
    {
        var ex = Should.Throw<FormatException>(() => Models.DataRange.FromPage(0, 2));
        ex.Message.ShouldBe("The value '0' is not a valid 'PageNumber'. PageNumber must be at least 1.");
    }

    [Fact]
    public void PageNumber_negative_throws_FormatException() =>
        Should.Throw<FormatException>(() => (Models.PageNumber)(-1));

    [Fact]
    public void PageNumber_one_is_valid()
    {
        Models.PageNumber page = 1;
        page.Value.ShouldBe(1);
    }

    // === OffsetSkip ===

    [Fact]
    public void OffsetSkip_negative_throws_FormatException_with_the_same_message_as_Storage()
    {
        var ex = Should.Throw<FormatException>(() => Models.DataRange.FromOffset(-1, 2));
        ex.Message.ShouldBe("The value '-1' is not a valid 'OffsetSkip'. Start position must be at least 0.");
    }

    [Fact]
    public void OffsetSkip_zero_is_valid()
    {
        Models.OffsetSkip skip = 0L;
        skip.Value.ShouldBe(0L);
    }

    // === DataRangeSize ===

    [Fact]
    public void DataRangeSize_zero_throws_FormatException()
    {
        var ex = Should.Throw<FormatException>(() => Models.DataRange.FromPage(1, 0));
        ex.Message.ShouldBe("The value '0' is not a valid 'DataRangeSize'. Page size must be at least 1.");
    }

    [Fact]
    public void DataRangeSize_above_MaxValue_throws_FormatException()
    {
        var ex = Should.Throw<FormatException>(() => Models.DataRange.FromPage(1, Models.DataRangeSize.MaxValue + 1));
        ex.Message.ShouldBe("The value '1001' is not a valid 'DataRangeSize'. Page size must not exceed 1000.");
    }

    [Fact]
    public void DataRangeSize_MaxValue_is_1000() => Models.DataRangeSize.MaxValue.ShouldBe(1000);

    [Fact]
    public void DataRangeSize_Default_is_25() => Models.DataRangeSize.Default.Value.ShouldBe(25);

    // === ContinuationToken ===

    [Fact]
    public void ContinuationToken_empty_string_throws_FormatException() =>
        Should.Throw<FormatException>(() => Models.ContinuationToken.Create(""));

    [Fact]
    public void ContinuationToken_Beginning_constant_is_a_valid_token()
    {
        Models.ContinuationToken token = Models.ContinuationToken.Beginning;
        token.Value.ShouldBe("Beginning");
    }

    // === DataRange: implicit conversions and factory round trips ===

    [Fact]
    public void FromPage_with_implicit_int_conversions_sets_PageValue()
    {
        var range = Models.DataRange.FromPage(2, 10);

        range.PageValue.ShouldNotBeNull();
        range.PageValue!.Page.Value.ShouldBe(2);
        range.PageValue.PageSize.Value.ShouldBe(10);
        range.OffsetValue.ShouldBeNull();
        range.TokenValue.ShouldBeNull();
    }

    [Fact]
    public void FromOffset_with_implicit_long_conversions_sets_OffsetValue()
    {
        var range = Models.DataRange.FromOffset(5, 3);

        range.OffsetValue.ShouldNotBeNull();
        range.OffsetValue!.Skip.Value.ShouldBe(5L);
        range.OffsetValue.Take.Value.ShouldBe(3);
    }

    [Fact]
    public void FromContinuationToken_with_implicit_string_conversion_sets_TokenValue()
    {
        var range = Models.DataRange.FromContinuationToken((Models.ContinuationToken)"abc", 7);

        range.TokenValue.ShouldNotBeNull();
        range.TokenValue!.Start.Value.ShouldBe("abc");
        range.TokenValue.Size.Value.ShouldBe(7);
    }

    [Fact]
    public void PagedDataRange_implicitly_converts_to_DataRange()
    {
        Models.DataRange range = Models.PagedDataRange.First();

        range.PageValue.ShouldNotBeNull();
        range.PageValue!.Page.Value.ShouldBe(1);
    }

    [Fact]
    public void OffsetDataRange_implicitly_converts_to_DataRange()
    {
        Models.DataRange range = Models.OffsetDataRange.First();

        range.OffsetValue.ShouldNotBeNull();
        range.OffsetValue!.Skip.Value.ShouldBe(0L);
    }

    [Fact]
    public void ContinuationTokenDataRange_implicitly_converts_to_DataRange()
    {
        Models.DataRange range = Models.ContinuationTokenDataRange.Beginning();

        range.TokenValue.ShouldNotBeNull();
        range.TokenValue!.Start.ShouldBe(Models.ContinuationToken.Beginning);
    }

    // === InvalidContinuationTokenException ===

    [Fact]
    public void InvalidContinuationTokenException_derives_from_ArgumentException_with_dataRange_param_name()
    {
        var ex = new Models.InvalidContinuationTokenException(Models.InvalidContinuationTokenReason.Malformed, "bad token");

        ex.ShouldBeAssignableTo<ArgumentException>();
        ex.ParamName.ShouldBe("dataRange");
        ex.Reason.ShouldBe(Models.InvalidContinuationTokenReason.Malformed);
    }
}
