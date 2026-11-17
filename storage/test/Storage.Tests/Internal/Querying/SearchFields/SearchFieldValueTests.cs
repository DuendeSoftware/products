// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Querying.SearchFields;

public sealed class SearchFieldValueTests
{
    [Fact]
    public void string_scalar_accepts_value_at_the_maximum_length()
    {
        var value = new string('a', SearchFieldValueLimits.MaxStringValueLength);

        var field = SearchFieldValue.String("path", value);

        field.StringValue.ShouldBe(value.ToUpperInvariant());
    }

    [Fact]
    public void string_scalar_refuses_value_longer_than_the_maximum()
    {
        var value = new string('a', SearchFieldValueLimits.MaxStringValueLength + 1);

        var ex = Should.Throw<ArgumentException>(() => SearchFieldValue.String("path", value));
        ex.ParamName.ShouldBe("value");
    }

    [Fact]
    public void string_array_item_accepts_value_at_the_maximum_length()
    {
        var value = new string('a', SearchFieldValueLimits.MaxStringValueLength);

        var field = SearchFieldValue.String("path", value, itemIndex: 0);

        field.StringValue.ShouldBe(value.ToUpperInvariant());
    }

    [Fact]
    public void string_array_item_refuses_value_longer_than_the_maximum()
    {
        var value = new string('a', SearchFieldValueLimits.MaxStringValueLength + 1);

        var ex = Should.Throw<ArgumentException>(() => SearchFieldValue.String("path", value, itemIndex: 0));
        ex.ParamName.ShouldBe("value");
    }

    [Fact]
    public void string_scalar_refuses_a_value_containing_a_null_character()
    {
        var value = "hel\0lo";

        var ex = Should.Throw<ArgumentException>(() => SearchFieldValue.String("path", value));
        ex.ParamName.ShouldBe("value");
    }

    [Fact]
    public void string_array_item_refuses_a_value_containing_a_null_character()
    {
        var value = "hel\0lo";

        var ex = Should.Throw<ArgumentException>(() => SearchFieldValue.String("path", value, itemIndex: 0));
        ex.ParamName.ShouldBe("value");
    }
}
