// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Storage.EntityAttributeValue.Internal.Storage;

namespace Duende.Storage.EntityAttributeValue.Storage;

/// <summary>
///     Verifies that all <see cref="AttributeType"/> subtypes survive a round-trip
///     through the <see cref="AttributeTypeDso"/> layer (and JSON serialization).
/// </summary>
public static class AttributeTypeDsoRoundTripTests
{
    private static AttributeTypeDso ToTypeDso(AttributeType type) =>
        type switch
        {
            ScalarAttributeType scalar => new AttributeTypeDso(
                "Scalar", scalar.DataType.ToString(), null, null, null, null),
            ComplexAttributeType complex => new AttributeTypeDso(
                "Complex", null, null, null,
                complex.Properties.ToDictionary(kvp => kvp.Key.Value, kvp => new ComplexPropertyDso(ToTypeDso(kvp.Value.Type), kvp.Value.DisplayName?.Value, kvp.Value.Description?.Value)),
                null),
            ListAttributeType list => new AttributeTypeDso(
                "List", null, null, null, null, ToTypeDso(list.ElementType)),
            _ => throw new InvalidOperationException()
        };

    private static AttributeType ToTypeValueObject(AttributeTypeDso dso) =>
        dso.Kind switch
        {
            "Scalar" => new ScalarAttributeType(Enum.Parse<ScalarDataType>(dso.ScalarDataType!)),
            "Complex" => new ComplexAttributeType(
                (dso.Properties ?? []).ToDictionary(
                    kvp => AttributeCode.Create(kvp.Key),
                    kvp => ComplexAttributeProperty.Of(
                        ToTypeValueObject(kvp.Value.Type),
                        kvp.Value.DisplayName is not null ? AttributeDisplayName.Create(kvp.Value.DisplayName) : (AttributeDisplayName?)null,
                        kvp.Value.Description is not null ? AttributeDescription.Create(kvp.Value.Description) : (AttributeDescription?)null))),
            "List" => new ListAttributeType(ToTypeValueObject(dso.ElementType!)),
            _ => throw new InvalidOperationException()
        };

    private static AttributeType RoundTrip(AttributeType original)
    {
        var dso = ToTypeDso(original);
        // Simulate JSON serialization round-trip (what the store would do)
        var json = JsonSerializer.Serialize(dso);
        var deserialized = JsonSerializer.Deserialize<AttributeTypeDso>(json);
        return ToTypeValueObject(deserialized!);
    }

    [Fact]
    public static void scalar_type_round_trips()
    {
        var original = new ScalarAttributeType(ScalarDataType.String);

        var result = RoundTrip(original);

        result.ShouldBe(original);
    }

    [Fact]
    public static void complex_type_round_trips()
    {
        var original = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("zip")] = ComplexAttributeProperty.Of(ScalarDataType.Integer)
        });

        var result = RoundTrip(original);

        _ = result.ShouldBeOfType<ComplexAttributeType>();
        var resultComplex = (ComplexAttributeType)result;
        resultComplex.Properties.Count.ShouldBe(2);
        _ = resultComplex.Properties[AttributeCode.Create("city")].Type.ShouldBeOfType<ScalarAttributeType>();
        _ = resultComplex.Properties[AttributeCode.Create("zip")].Type.ShouldBeOfType<ScalarAttributeType>();
    }

    [Fact]
    public static void list_of_complex_type_round_trips()
    {
        var original = new ListAttributeType(new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("number")] = ComplexAttributeProperty.Of(ScalarDataType.String),
            [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String)
        }));

        var result = RoundTrip(original);

        _ = result.ShouldBeOfType<ListAttributeType>();
        var resultList = (ListAttributeType)result;
        _ = resultList.ElementType.ShouldBeOfType<ComplexAttributeType>();
        var resultElement = (ComplexAttributeType)resultList.ElementType;
        resultElement.Properties.Count.ShouldBe(2);
    }

    [Fact]
    public static void complex_property_metadata_round_trips()
    {
        var original = new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
        {
            [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(
                new ScalarAttributeType(ScalarDataType.String),
                AttributeDisplayName.Create("City"),
                AttributeDescription.Create("The city name")),
            [AttributeCode.Create("zip")] = ComplexAttributeProperty.Of(
                new ScalarAttributeType(ScalarDataType.Integer),
                AttributeDisplayName.Create("ZIP Code"),
                null)
        });

        var result = RoundTrip(original);

        _ = result.ShouldBeOfType<ComplexAttributeType>();
        var resultComplex = (ComplexAttributeType)result;
        resultComplex.Properties.Count.ShouldBe(2);

        var city = resultComplex.Properties[AttributeCode.Create("city")];
        _ = city.Type.ShouldBeOfType<ScalarAttributeType>();
        city.DisplayName.ShouldBe(AttributeDisplayName.Create("City"));
        city.Description.ShouldBe(AttributeDescription.Create("The city name"));

        var zip = resultComplex.Properties[AttributeCode.Create("zip")];
        _ = zip.Type.ShouldBeOfType<ScalarAttributeType>();
        zip.DisplayName.ShouldBe(AttributeDisplayName.Create("ZIP Code"));
        zip.Description.ShouldBeNull();
    }
}
