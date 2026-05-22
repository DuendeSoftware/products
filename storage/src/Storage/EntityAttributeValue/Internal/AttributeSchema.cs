// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying;

namespace Duende.Storage.EntityAttributeValue.Internal;

/// <summary>
///     Represents a dynamic collection of attributes.
/// </summary>
internal sealed class AttributeSchema : IReadOnlyAttributeSchema
{
    private readonly Dictionary<AttributeCode, AttributeDefinition> _attributesDefinitions;
    private readonly Dictionary<AttributeGroupCode, AttributeGroup> _groups;

    private AttributeSchema(IEnumerable<AttributeDefinition> attributeDefinitions, IEnumerable<AttributeGroup> groups,
        UuidV7 schemaId, int version)
    {
        SchemaId = schemaId;
        Version = version;

        _attributesDefinitions = new Dictionary<AttributeCode, AttributeDefinition>();
        foreach (var d in attributeDefinitions)
        {
            _attributesDefinitions[d.Code] = d; // Last-write-wins for duplicates from storage
        }

        _groups = new Dictionary<AttributeGroupCode, AttributeGroup>();
        foreach (var g in groups)
        {
            _groups[g.Code] = g; // Last-write-wins for duplicates from storage
        }
    }

    private AttributeSchema(IEnumerable<AttributeDefinition> attributeDefinitions, IEnumerable<AttributeGroup> groups)
        : this(attributeDefinitions, groups, UuidV7.Load(Guid.Empty), 0)
    {
    }

    public static AttributeSchema Empty { get; } = new([], []);

    internal UuidV7 SchemaId { get; private set; }

    internal int Version { get; private set; }

    public IReadOnlyDictionary<AttributeCode, AttributeDefinition> AttributeDefinitions => _attributesDefinitions;

    public IReadOnlyDictionary<AttributeGroupCode, AttributeGroup> Groups => _groups;

    public bool AddGroup(AttributeGroup group) => _groups.TryAdd(group.Code, group);

    public bool RemoveGroup(AttributeGroupCode name)
    {
        if (!_groups.Remove(name))
        {
            return false;
        }

        // Ungroup all attributes that referenced this group
        var toUngroup = _attributesDefinitions.Values
            .Where(d => d.GroupCode != null && d.GroupCode.Equals(name))
            .ToList();

        foreach (var definition in toUngroup)
        {
            _attributesDefinitions[definition.Code] = AttributeDefinition.Load(
                definition.Code,
                definition.AttributeType,
                definition.Description,
                definition.DisplayName,
                definition.IsUnique,
                definition.IsQueryable,
                definition.IsRequired,
                definition.Tags,
                null,
                definition.Order);
        }

        return true;
    }

    public bool UpdateGroup(AttributeGroup group)
    {
        if (!_groups.ContainsKey(group.Code))
        {
            return false;
        }

        _groups[group.Code] = group;
        return true;
    }

    public bool AddAttributeDefinition(AttributeDefinition definition)
    {
        if (SystemFields.IsReservedAttributeName(definition.Code.Value))
        {
            return false;
        }

        if (definition.IsUnique && definition.AttributeType is ComplexAttributeType or ListAttributeType)
        {
            return false;
        }

        if (definition.GroupCode != null && !_groups.ContainsKey(definition.GroupCode))
        {
            return false;
        }

        return _attributesDefinitions.TryAdd(definition.Code, definition);
    }

    public void RemoveAttributeDefinition(AttributeCode code) => _ = _attributesDefinitions.Remove(code);

    public static AttributeSchema Load(IEnumerable<AttributeDefinition> attributes) => new(attributes, []);

    public static AttributeSchema Load(IEnumerable<AttributeDefinition> attributes, IEnumerable<AttributeGroup> groups) => new(attributes, groups);

    public static AttributeSchema Load(IEnumerable<AttributeDefinition> attributes, IEnumerable<AttributeGroup> groups, UuidV7 schemaId, int version) =>
        new(attributes, groups, schemaId, version);
}
