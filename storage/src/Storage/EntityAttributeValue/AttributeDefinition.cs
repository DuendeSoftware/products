// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

public sealed record AttributeDefinition
{
    public AttributeDefinition()
    {
    }

    public static AttributeDefinition Load(
        AttributeCode code,
        AttributeType attributeType,
        AttributeDescription? description,
        AttributeDisplayName? displayName,
        bool isUnique,
        bool isQueryable,
        bool isRequired,
        IReadOnlyCollection<string> tags,
        AttributeGroupCode? groupCode,
        int order) =>
        new()
        {
            Code = code,
            AttributeType = attributeType,
            Description = description,
            DisplayName = displayName,
            IsUnique = isUnique,
            IsQueryable = isQueryable,
            IsRequired = isRequired,
            Tags = tags,
            GroupCode = groupCode,
            Order = order
        };

    public required AttributeCode Code { get; init; }

    /// <summary>
    ///     The full type descriptor for this attribute.
    /// </summary>
    public required AttributeType AttributeType { get; init; }

    /// <summary>
    ///     Convenience accessor for scalar attribute types.
    ///     Throws <see cref="InvalidOperationException" /> for non-scalar types.
    /// </summary>
    public ScalarDataType DataType =>
        AttributeType is ScalarAttributeType scalar
            ? scalar.DataType
            : throw new InvalidOperationException(
                $"Attribute '{Code}' has type '{AttributeType.GetType().Name}', not a scalar type. Use AttributeType instead.");

    public AttributeDescription? Description { get; init; }
    public AttributeDisplayName? DisplayName { get; init; }

    /// <summary>
    ///     Controls whether the attribute's values can be used in queries (filtering, sorting, projection).
    /// </summary>
    /// <remarks>
    ///     Only queryable attributes can be filtered, sorted, or projected via store queries
    ///     (e.g., <c>QueryFieldsAsync</c> or <c>QueryAsync</c> with a filter expression).
    ///     Non-queryable attributes are still stored in the entity's JSON payload and can be
    ///     read when loading the full entity, but their values will not appear in projected
    ///     query results and cannot be used in filter or sort expressions.
    /// </remarks>
    public bool IsQueryable { get; init; } = true;

    public bool IsRequired { get; init; }
    public bool IsUnique { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; } = [];

    /// <summary>
    ///     The group this attribute belongs to, or <c>null</c> if ungrouped.
    /// </summary>
    public AttributeGroupCode? GroupCode { get; init; }

    /// <summary>
    ///     Sort weight controlling display order within the group (or among ungrouped attributes).
    ///     Not required to be unique; ties are resolved by a stable secondary sort (e.g. name).
    /// </summary>
    public int Order { get; init; }

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
                $"Code = {Code}, AttributeType = {AttributeType}, Description = {Description?.Value ?? "(none)"}, DisplayName = {DisplayName?.Value ?? "(none)"}, IsUnique = {IsUnique}, IsQueryable = {IsQueryable}, IsRequired = {IsRequired}, Tags = [{string.Join(", ", Tags)}], GroupCode = {GroupCode?.Value ?? "(none)"}, Order = {Order}"));
        return true;
    }
}
