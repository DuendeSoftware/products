// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml;

/// <summary>
/// AttributeStatement, Core 2.7.3
/// </summary>
public class AttributeStatement : List<SamlAttribute>
{
    /// <summary>
    /// Convenience add method to add attribute.
    /// </summary>
    /// <param name="attributeName">Name of the attribute</param>
    /// <param name="attributeValues">Values of the attribute</param>
    public void Add(string attributeName, params string?[] attributeValues)
        => Add(new SamlAttribute() { Name = attributeName, Values = [.. attributeValues] });
}
