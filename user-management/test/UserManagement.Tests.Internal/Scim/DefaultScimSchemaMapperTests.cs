// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
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
        result.CaseExact.ShouldBeTrue();
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
        result.CaseExact.ShouldBeTrue();
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
        result.CaseExact.ShouldBeTrue();
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
    [InlineData(ScalarDataType.Boolean, "boolean")]
    [InlineData(ScalarDataType.Date, "dateTime")]
    [InlineData(ScalarDataType.DateTime, "dateTime")]
    [InlineData(ScalarDataType.Decimal, "decimal")]
    [InlineData(ScalarDataType.Integer, "integer")]
    [InlineData(ScalarDataType.String, "string")]
    public void MapDataTypeReturnsCorrectScimType(ScalarDataType dataType, string expectedScimType) =>
        DefaultScimSchemaMapper.MapDataType(dataType).ShouldBe(expectedScimType);
}
