// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement.Scim;
using Duende.UserManagement.Scim.Internal;

namespace Duende.Platform.UserManagement.Scim;

public sealed class DefaultScimSchemaMapperTests
{
    private readonly DefaultScimSchemaMapper _mapper = new();

    [Fact]
    public void MapStringAttributeReturnsStringType()
    {
        var definition = new AttributeDefinition
        {
            Code = AttributeCode.Create("email"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("Email address")
        };

        var result = _mapper.Map(definition);

        result.Name.ShouldBe("email");
        result.Type.ShouldBe("string");
        result.MultiValued.ShouldBeFalse();
        result.Description.ShouldBe("Email address");
        result.Required.ShouldBeFalse();
        result.CaseExact.ShouldBeFalse();
        result.Mutability.ShouldBe("readWrite");
        result.Returned.ShouldBe("default");
        result.Uniqueness.ShouldBe("none");
    }

    [Fact]
    public void MapBooleanAttributeReturnsBooleanType()
    {
        var definition = new AttributeDefinition
        {
            Code = AttributeCode.Create("active"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
            Description = AttributeDescription.Create("Active status")
        };

        var result = _mapper.Map(definition);

        result.Type.ShouldBe("boolean");
        result.CaseExact.ShouldBeFalse();
    }

    [Fact]
    public void MapIntegerAttributeReturnsIntegerType()
    {
        var definition = new AttributeDefinition
        {
            Code = AttributeCode.Create("age"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Integer),
            Description = AttributeDescription.Create("User age")
        };

        var result = _mapper.Map(definition);

        result.Type.ShouldBe("integer");
        result.CaseExact.ShouldBeFalse();
    }

    [Fact]
    public void MapDecimalAttributeReturnsDecimalType()
    {
        var definition = new AttributeDefinition
        {
            Code = AttributeCode.Create("score"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Decimal),
            Description = AttributeDescription.Create("User score")
        };

        var result = _mapper.Map(definition);

        result.Type.ShouldBe("decimal");
        result.CaseExact.ShouldBeFalse();
    }

    [Fact]
    public void MapDateAttributeReturnsDateTimeType()
    {
        var definition = new AttributeDefinition
        {
            Code = AttributeCode.Create("birthdate"),
            AttributeType = new ScalarAttributeType(ScalarDataType.Date),
            Description = AttributeDescription.Create("Birth date")
        };

        var result = _mapper.Map(definition);

        result.Type.ShouldBe("dateTime");
    }

    [Fact]
    public void MapDateTimeAttributeReturnsDateTimeType()
    {
        var definition = new AttributeDefinition
        {
            Code = AttributeCode.Create("recordedat"),
            AttributeType = new ScalarAttributeType(ScalarDataType.DateTime),
            Description = AttributeDescription.Create("Created at timestamp")
        };

        var result = _mapper.Map(definition);

        result.Type.ShouldBe("dateTime");
    }

    [Fact]
    public void MapUniqueAttributeReturnsServerUniqueness()
    {
        var definition = new AttributeDefinition
        {
            Code = AttributeCode.Create("employeeid"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("Employee ID"),
            IsUnique = true
        };

        var result = _mapper.Map(definition);

        result.Uniqueness.ShouldBe("server");
    }

    [Fact]
    public void MapNonUniqueAttributeReturnsNoneUniqueness()
    {
        var definition = new AttributeDefinition
        {
            Code = AttributeCode.Create("department"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("Department name"),
            IsUnique = false
        };

        var result = _mapper.Map(definition);

        result.Uniqueness.ShouldBe("none");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MapRequiredAttributeReturnsDefinitionRequirement(bool isRequired)
    {
        var definition = new AttributeDefinition
        {
            Code = AttributeCode.Create("department"),
            AttributeType = new ScalarAttributeType(ScalarDataType.String),
            Description = AttributeDescription.Create("Department name"),
            IsRequired = isRequired
        };

        var result = _mapper.Map(definition);

        result.Required.ShouldBe(isRequired);
    }

    [Theory]
    [InlineData(ScalarDataType.Boolean, "boolean")]
    [InlineData(ScalarDataType.Date, "dateTime")]
    [InlineData(ScalarDataType.DateTime, "dateTime")]
    [InlineData(ScalarDataType.Decimal, "decimal")]
    [InlineData(ScalarDataType.Integer, "integer")]
    [InlineData(ScalarDataType.String, "string")]
    public void MapDataTypeReturnsCorrectScimType(ScalarDataType dataType, string expectedScimType) =>
        DefaultScimSchemaMapper.MapDataType(dataType).ShouldBe(expectedScimType);

    [Fact]
    public void map_uses_required_flag_and_maps_complex_subattributes()
    {
        var definition = DefaultScimUserSchema.AttributeDefinitions.Single(candidate =>
            candidate.Code == AttributeCode.Create("emails"));

        var result = _mapper.Map(definition);

        result.Type.ShouldBe("complex");
        result.MultiValued.ShouldBeTrue();
        _ = result.SubAttributes.ShouldNotBeNull();
        result.SubAttributes.Select(attribute => attribute.Name).ShouldBe(
            ["value", "display", "type", "primary"],
            ignoreOrder: true);
        result.SubAttributes.Single(attribute => attribute.Name == "primary").Type.ShouldBe("boolean");
    }

    [Theory]
    [InlineData("profileUrl", "string", "readWrite", "default")]
    public void map_applies_standard_scim_metadata(
        string attributeName,
        string expectedType,
        string expectedMutability,
        string expectedReturned)
    {
        var definition = DefaultScimUserSchema.AttributeDefinitions.Single(candidate =>
            candidate.Code == AttributeCode.Create(attributeName));

        var result = _mapper.Map(definition);

        result.Type.ShouldBe(expectedType);
        result.Mutability.ShouldBe(expectedMutability);
        result.Returned.ShouldBe(expectedReturned);
    }

    [Fact]
    public void map_uses_definition_required_flag()
    {
        var definition = DefaultScimUserSchema.AttributeDefinitions.Single(candidate =>
            candidate.Code == AttributeCode.Create("userName"));

        _mapper.Map(definition).Required.ShouldBeTrue();
    }

    [Fact]
    public void map_maps_x509_certificate_value_as_binary()
    {
        var definition = DefaultScimUserSchema.AttributeDefinitions.Single(candidate =>
            candidate.Code == AttributeCode.Create("x509Certificates"));

        var result = _mapper.Map(definition);
        var value = result.SubAttributes.ShouldNotBeNull().Single(attribute => attribute.Name == "value");

        value.Type.ShouldBe("binary");
        value.CaseExact.ShouldBeFalse();
    }
}
