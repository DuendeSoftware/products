// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement.Scim.Internal;

namespace Duende.UserManagement.Scim;

/// <summary>
/// Maps a platform <see cref="AttributeDefinition"/> to a SCIM schema attribute
/// representation. Implement this interface to customise how profile schema attributes
/// are exposed through the SCIM /Schemas endpoint (e.g. to provide mutability,
/// returned, or caseExact metadata not yet present on <see cref="AttributeDefinition"/>).
/// </summary>
public interface IScimSchemaMapper
{
    /// <summary>
    /// Maps a single <see cref="AttributeDefinition"/> to a SCIM schema attribute model.
    /// </summary>
    /// <param name="definition">The platform schema attribute definition.</param>
    /// <returns>A SCIM schema attribute representation.</returns>
    ScimSchemaAttributeModel Map(AttributeDefinition definition);
}

/// <summary>
/// Represents a SCIM schema attribute as returned by the /Schemas endpoint (RFC 7643 §7).
/// </summary>
public sealed class ScimSchemaAttributeModel
{
    /// <summary>The attribute's name.</summary>
    public required string Name { get; init; }

    /// <summary>The attribute's data type (e.g. "string", "boolean", "integer", "decimal", "dateTime").</summary>
    public required string Type { get; init; }

    /// <summary>Whether the attribute is multi-valued.</summary>
    public bool MultiValued { get; init; }

    /// <summary>A human-readable description of the attribute.</summary>
    public string? Description { get; init; }

    /// <summary>Whether the attribute value must be treated as required on create.</summary>
    public bool Required { get; init; }

    /// <summary>Whether string values are case-exact.</summary>
    public bool CaseExact { get; init; }

    /// <summary>The attribute's mutability (e.g. "readOnly", "readWrite", "immutable", "writeOnly").</summary>
    public string Mutability { get; init; } = ScimConstants.MutabilityValues.ReadWrite;

    /// <summary>When the attribute is returned (e.g. "always", "never", "default", "request").</summary>
    public string Returned { get; init; } = ScimConstants.ReturnedValues.Default;

    /// <summary>The uniqueness constraint (e.g. "none", "server", "global").</summary>
    public string Uniqueness { get; init; } = ScimConstants.UniquenessValues.None;
}
