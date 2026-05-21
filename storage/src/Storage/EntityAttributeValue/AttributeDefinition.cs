// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public sealed record AttributeDefinition
{
    /// <summary>
    ///     Creates a definition with a scalar data type.
    /// </summary>
    public AttributeDefinition(
        AttributeCode Code,
        ScalarDataType DataType,
        AttributeDescription? Description,
        bool IsUnique,
        IReadOnlyCollection<string>? Tags)
        : this(Code, new ScalarAttributeType(DataType), Description, IsUnique, Tags, null, 0)
    {
    }

    /// <summary>
    ///     Creates a definition with a scalar data type and IsUnique flag.
    /// </summary>
    public AttributeDefinition(
        AttributeCode Code,
        ScalarDataType DataType,
        AttributeDescription? Description,
        bool IsUnique)
        : this(Code, new ScalarAttributeType(DataType), Description, IsUnique, null, null, 0)
    {
    }

    /// <summary>
    ///     Creates a definition with a scalar data type (convenience overload without IsUnique/Tags).
    /// </summary>
    public AttributeDefinition(
        AttributeCode Code,
        ScalarDataType DataType,
        AttributeDescription? Description)
        : this(Code, new ScalarAttributeType(DataType), Description, false, null, null, 0)
    {
    }

    /// <summary>
    ///     Creates a definition with any <see cref="AttributeType" />.
    /// </summary>
    public AttributeDefinition(
        AttributeCode Code,
        AttributeType AttributeType,
        AttributeDescription? Description,
        bool IsUnique,
        IReadOnlyCollection<string>? Tags)
        : this(Code, AttributeType, Description, IsUnique, Tags, null, 0)
    {
    }

    /// <summary>
    ///     Creates a definition with any <see cref="AttributeType" /> (convenience overload without IsUnique/Tags).
    /// </summary>
    public AttributeDefinition(
        AttributeCode Code,
        AttributeType AttributeType,
        AttributeDescription? Description)
        : this(Code, AttributeType, Description, false, null, null, 0)
    {
    }

    /// <summary>
    ///     Creates a definition with a scalar data type, group, and order.
    /// </summary>
    public AttributeDefinition(
        AttributeCode Code,
        ScalarDataType DataType,
        AttributeDescription? Description,
        bool IsUnique,
        IReadOnlyCollection<string>? Tags,
        AttributeGroupCode? GroupCode,
        int Order)
        : this(Code, new ScalarAttributeType(DataType), Description, IsUnique, Tags, GroupCode, Order)
    {
    }

    /// <summary>
    ///     Creates a definition with any <see cref="AttributeType" />, group, and order.
    /// </summary>
    public AttributeDefinition(
        AttributeCode Code,
        AttributeType AttributeType,
        AttributeDescription? Description,
        bool IsUnique,
        IReadOnlyCollection<string>? Tags,
        AttributeGroupCode? GroupCode,
        int Order)
    {
        if (IsUnique && AttributeType is ComplexAttributeType or ListAttributeType)
        {
            throw new ArgumentException(
                "IsUnique is not supported for complex or list attribute types.",
                nameof(IsUnique));
        }

        this.Code = Code;
        this.AttributeType = AttributeType;
        this.Description = Description;
        this.IsUnique = IsUnique;
        this.Tags = Tags ?? [];
        this.GroupCode = GroupCode;
        this.Order = Order;
    }

    public static AttributeDefinition Load(
        AttributeCode code,
        AttributeType attributeType,
        AttributeDescription? description,
        bool isUnique,
        IReadOnlyCollection<string> tags,
        AttributeGroupCode? groupCode,
        int order) =>
        Load(code, attributeType, description, isUnique, tags, groupCode, order, null);

    public static AttributeDefinition Load(
        AttributeCode code,
        AttributeType attributeType,
        AttributeDescription? description,
        bool isUnique,
        IReadOnlyCollection<string> tags,
        AttributeGroupCode? groupCode,
        int order,
        AttributeDisplayName? displayName) =>
        new(code, attributeType, description, isUnique, tags, groupCode, order)
        {
            DisplayName = displayName
        };

    public AttributeCode Code { get; }

    /// <summary>
    ///     The full type descriptor for this attribute.
    /// </summary>
    public AttributeType AttributeType { get; }

    /// <summary>
    ///     Convenience accessor for scalar attribute types.
    ///     Throws <see cref="InvalidOperationException" /> for non-scalar types.
    /// </summary>
    public ScalarDataType DataType =>
        AttributeType is ScalarAttributeType scalar
            ? scalar.DataType
            : throw new InvalidOperationException(
                $"Attribute '{Code}' has type '{AttributeType.GetType().Name}', not a scalar type. Use AttributeType instead.");

    public AttributeDescription? Description { get; }
    public AttributeDisplayName? DisplayName { get; init; }
    public bool IsUnique { get; }
    public IReadOnlyCollection<string> Tags { get; }

    /// <summary>
    ///     The group this attribute belongs to, or <c>null</c> if ungrouped.
    /// </summary>
    public AttributeGroupCode? GroupCode { get; }

    /// <summary>
    ///     Sort weight controlling display order within the group (or among ungrouped attributes).
    ///     Not required to be unique; ties are resolved by a stable secondary sort (e.g. name).
    /// </summary>
    public int Order { get; }

#pragma warning disable CA2225 // Operator overloads have named alternates
    public static implicit operator AttributeCode(AttributeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Code;
    }
#pragma warning restore CA2225

    /// <summary>
    ///     Replaces the compiler-generated <c>PrintMembers</c> (private for sealed records)
    ///     to avoid calling <see cref="DataType" />, which throws for non-scalar attribute types.
    /// </summary>
    private bool PrintMembers(System.Text.StringBuilder builder)
    {
        _ = builder.Append(
            System.FormattableString.Invariant(
                $"Code = {Code}, AttributeType = {AttributeType}, Description = {Description?.Value ?? "(none)"}, DisplayName = {DisplayName?.Value ?? "(none)"}, IsUnique = {IsUnique}, Tags = [{string.Join(", ", Tags)}], GroupCode = {GroupCode?.Value ?? "(none)"}, Order = {Order}"));
        return true;
    }
}
