// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.EntityAttributeValue;

/// <summary>
///     Helpers for deriving new schemas from an existing <see cref="SchemaConfiguration"/>.
/// </summary>
public static class SchemaConfigurationExtensions
{
    /// <summary>
    ///     Returns a new <see cref="SchemaConfiguration"/> with <paramref name="attributes"/> added to its attribute definitions.
    /// </summary>
    /// <example>
    ///     <code>
    ///     var extended = BuiltInSchemas.UserProfile.Extend(department);
    ///     </code>
    /// </example>
    /// <inheritdoc cref="Extend(SchemaConfiguration, IEnumerable{AttributeDefinition}, IEnumerable{AttributeGroup})"/>
    public static SchemaConfiguration Extend(this SchemaConfiguration schema, params IEnumerable<AttributeDefinition> attributes) =>
        schema.Extend(attributes, []);

    /// <summary>
    ///     Returns a new <see cref="SchemaConfiguration"/> that has the same <see cref="SchemaConfiguration.SchemaId"/>,
    ///     <see cref="SchemaConfiguration.DisplayName"/>, <see cref="SchemaConfiguration.Description"/> and
    ///     <see cref="SchemaConfiguration.Version"/> as <paramref name="schema"/>, with <paramref name="attributes"/>
    ///     added to its attribute definitions and <paramref name="groups"/> added to its groups. The original
    ///     <paramref name="schema"/> is not modified.
    /// </summary>
    /// <param name="schema">The schema to extend.</param>
    /// <param name="attributes">The attribute definitions to add.</param>
    /// <param name="groups">The attribute groups to add.</param>
    /// <returns>A new <see cref="SchemaConfiguration"/> with the additional attribute definitions and groups.</returns>
    /// <exception cref="InvalidOperationException">
    ///     An attribute code in <paramref name="attributes"/>, or a group code in <paramref name="groups"/>, already
    ///     exists in <paramref name="schema"/>, or is repeated in the arguments. To change an existing attribute or
    ///     group, register a full replacement schema instead.
    /// </exception>
    /// <example>
    ///     <code>
    ///     var extended = BuiltInSchemas.UserProfile.Extend([department], [personalInfoGroup]);
    ///     </code>
    /// </example>
    public static SchemaConfiguration Extend(
        this SchemaConfiguration schema,
        IEnumerable<AttributeDefinition> attributes,
        IEnumerable<AttributeGroup> groups)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(groups);

        var newAttributes = attributes.ToList();
        var newGroups = groups.ToList();

        var existingAttributeCodes = new HashSet<AttributeCode>(schema.AttributeDefinitions.Select(a => a.Code));
        foreach (var attribute in newAttributes)
        {
            if (!existingAttributeCodes.Add(attribute.Code))
            {
                throw new InvalidOperationException(
                    $"Attribute '{attribute.Code}' already exists in schema '{schema.SchemaId}'. " +
                    "Register a full replacement schema instead to change an existing attribute.");
            }
        }

        var existingGroupCodes = new HashSet<AttributeGroupCode>(schema.Groups.Select(g => g.Code));
        foreach (var group in newGroups)
        {
            if (!existingGroupCodes.Add(group.Code))
            {
                throw new InvalidOperationException(
                    $"Group '{group.Code}' already exists in schema '{schema.SchemaId}'. " +
                    "Register a full replacement schema instead to change an existing group.");
            }
        }

        return new SchemaConfiguration
        {
            SchemaId = schema.SchemaId,
            DisplayName = schema.DisplayName,
            Description = schema.Description,
            Version = schema.Version,
            AttributeDefinitions = [.. schema.AttributeDefinitions, .. newAttributes],
            Groups = [.. schema.Groups, .. newGroups]
        };
    }
}
