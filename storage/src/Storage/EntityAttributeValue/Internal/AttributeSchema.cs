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

        // Read-only views over the live dictionaries: they reflect later Add/Remove calls, but callers
        // can't cast them back to a mutable dictionary.
        AttributeDefinitions = _attributesDefinitions.AsReadOnly();
        Groups = _groups.AsReadOnly();
    }

    private AttributeSchema(IEnumerable<AttributeDefinition> attributeDefinitions, IEnumerable<AttributeGroup> groups)
        : this(attributeDefinitions, groups, UuidV7.Load(Guid.Empty), 0)
    {
    }

    /// <summary>
    /// Gets a shared schema with no attribute definitions and no groups.
    /// </summary>
    public static AttributeSchema Empty { get; } = new([], []);

    internal UuidV7 SchemaId { get; private set; }

    internal int Version { get; private set; }

    /// <inheritdoc />
    public IReadOnlyDictionary<AttributeCode, AttributeDefinition> AttributeDefinitions { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<AttributeGroupCode, AttributeGroup> Groups { get; }

    /// <summary>
    /// Adds an attribute group to the schema.
    /// </summary>
    /// <param name="group">The group to add.</param>
    /// <returns><c>true</c> if the group was added; <c>false</c> if a group with the same code already exists.</returns>
    public bool AddGroup(AttributeGroup group) => _groups.TryAdd(group.Code, group);

    /// <summary>
    /// Removes an attribute group from the schema and ungroups every attribute definition that referenced it.
    /// </summary>
    /// <param name="name">The code of the group to remove.</param>
    /// <returns><c>true</c> if the group was removed; <c>false</c> if no group with that code exists.</returns>
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

    /// <summary>
    /// Replaces an existing attribute group with the same code.
    /// </summary>
    /// <param name="group">The updated group.</param>
    /// <returns><c>true</c> if the group was replaced; <c>false</c> if no group with that code exists.</returns>
    public bool UpdateGroup(AttributeGroup group)
    {
        if (!_groups.ContainsKey(group.Code))
        {
            return false;
        }

        _groups[group.Code] = group;
        return true;
    }

    /// <summary>
    /// Adds an attribute definition to the schema.
    /// </summary>
    /// <param name="definition">The attribute definition to add.</param>
    /// <returns>
    /// <c>true</c> if the definition was added; <c>false</c> if its code is reserved, it is a unique complex or list
    /// attribute, it references a group that does not exist, or a definition with the same code already exists.
    /// </returns>
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

    /// <summary>
    /// Removes the attribute definition with the given code, if present.
    /// </summary>
    /// <param name="code">The code of the attribute definition to remove.</param>
    public void RemoveAttributeDefinition(AttributeCode code) => _ = _attributesDefinitions.Remove(code);

    /// <summary>
    /// Creates a schema from existing attribute definitions, without groups.
    /// </summary>
    /// <param name="attributes">The attribute definitions. For duplicate codes, the last definition wins.</param>
    /// <returns>The loaded schema.</returns>
    public static AttributeSchema Load(IEnumerable<AttributeDefinition> attributes) => new(attributes, []);

    /// <summary>
    /// Creates a schema from existing attribute definitions and groups.
    /// </summary>
    /// <param name="attributes">The attribute definitions. For duplicate codes, the last definition wins.</param>
    /// <param name="groups">The attribute groups. For duplicate codes, the last group wins.</param>
    /// <returns>The loaded schema.</returns>
    public static AttributeSchema Load(IEnumerable<AttributeDefinition> attributes, IEnumerable<AttributeGroup> groups) => new(attributes, groups);

    /// <summary>
    /// Creates a schema from persisted attribute definitions, groups, identity and version.
    /// </summary>
    /// <param name="attributes">The attribute definitions. For duplicate codes, the last definition wins.</param>
    /// <param name="groups">The attribute groups. For duplicate codes, the last group wins.</param>
    /// <param name="schemaId">The persisted schema identifier.</param>
    /// <param name="version">The persisted schema version.</param>
    /// <returns>The loaded schema.</returns>
    public static AttributeSchema Load(IEnumerable<AttributeDefinition> attributes, IEnumerable<AttributeGroup> groups, UuidV7 schemaId, int version) =>
        new(attributes, groups, schemaId, version);
}
