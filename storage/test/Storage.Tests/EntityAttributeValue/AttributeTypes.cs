// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public static class AttributeTypes
{
    [Theory]
    [InlineData(ScalarDataType.Boolean)]
    [InlineData(ScalarDataType.Date)]
    [InlineData(ScalarDataType.DateTime)]
    [InlineData(ScalarDataType.Decimal)]
    [InlineData(ScalarDataType.Integer)]
    [InlineData(ScalarDataType.String)]
    public static void ScalarCanBeConstructedForEachDataType(ScalarDataType dataType)
    {
        var type = new ScalarAttributeType(dataType);

        type.DataType.ShouldBe(dataType);
    }

    [Fact]
    public static void ScalarRejectsInvalidDataType()
    {
        var ex = Record.Exception(() => _ = new ScalarAttributeType((ScalarDataType)999));

        _ = ex.ShouldBeOfType<ArgumentException>();
    }

    [Fact]
    public static void ComplexCanBeConstructedWithProperties()
    {
        var type = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("zip")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });

        type.Properties.Count.ShouldBe(2);
    }

    [Fact]
    public static void ComplexRejectsEmptyProperties()
    {
        var ex = Record.Exception(() => _ = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>()));

        _ = ex.ShouldBeOfType<ArgumentException>();
    }

    [Fact]
    public static void ComplexAcceptsMixedCasePropertyNames()
    {
        var type = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("givenName")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("familyName")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });

        type.Properties.Count.ShouldBe(2);
    }

    [Fact]
    public static void ComplexProperties_lookup_is_case_insensitive()
    {
        var type = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("givenName")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        });

        type.TryGetProperty("givenname", out _, out _).ShouldBeTrue();
        type.TryGetProperty("GIVENNAME", out _, out _).ShouldBeTrue();
        type.TryGetProperty("GivenName", out _, out _).ShouldBeTrue();
    }

    [Fact]
    public static void ListOfScalarIsValid()
    {
        var type = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String));

        _ = type.ElementType.ShouldBeOfType<ScalarAttributeType>();
    }

    [Fact]
    public static void ListOfComplexIsValid()
    {
        var type = new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("name")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        }));

        _ = type.ElementType.ShouldBeOfType<ComplexAttributeType>();
    }

    [Fact]
    public static void DirectListInListThrows()
    {
        var innerList = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String));

        var ex = Record.Exception(() => _ = new ListAttributeType(innerList));

        _ = ex.ShouldBeOfType<ArgumentException>();
    }

    [Fact]
    public static void ComplexContainingListContainingComplexIsValid()
    {
        // address { phones: List<{ number: string }> }
        var type = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("phones")] = ComplexAttributeProperty.Of(
                new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
                {
                    [AttributeCode.Create("number")] = ComplexAttributeProperty.Of(ScalarDataType.String)
                })))
        });

        _ = type.ShouldNotBeNull();
    }

    [Fact]
    public static void ComplexContainingListContainingComplexContainingListThrows()
    {
        // Outer complex → list → inner complex → list (transitive list-in-list at depth 2)
        var ex = Record.Exception(() => _ = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("outer")] = ComplexAttributeProperty.Of(
                new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
                {
                    // This inner list would be inside an outer list → invalid
                    [AttributeCode.Create("inner")] = ComplexAttributeProperty.Of(
                        new ListAttributeType(new ScalarAttributeType(ScalarDataType.String)))
                })))
        }));

        _ = ex.ShouldBeOfType<ArgumentException>();
    }

    [Fact]
    public static void ListOfComplexContainingComplexIsValid()
    {
        // List<{ address: { city: string } }> — no nested list
        var type = new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("address")] = ComplexAttributeProperty.Of(
                new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
                {
                    [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String)
                }))
        }));

        _ = type.ShouldNotBeNull();
    }
}
