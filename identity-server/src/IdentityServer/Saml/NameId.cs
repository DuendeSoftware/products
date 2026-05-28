// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml;

/// <summary>
/// The Saml NameIDType
/// </summary>
public class NameId
{
    /// <summary>
    /// Ctor
    /// </summary>
    public NameId() { }

    /// <summary>
    /// Ctor
    /// </summary>
    public NameId(string value, string? format = null)
    {
        Value = value;
        Format = format;
    }

    /// <summary>
    /// A URI reference representing the classification of string-based identifier information.
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// The value, i.e. string contents of the XML node.
    /// </summary>
    public string Value { get; set; } = default!;

    /// <summary>
    /// Optionally qualifies a name identifier in the namespace of a service provider or affiliation of providers.
    /// </summary>
    public string? SPNameQualifier { get; set; }

    /// <summary>
    /// The security or administrative domain that qualifies the name.
    /// </summary>
    public string? NameQualifier { get; set; }

    /// <summary>
    /// Implicit operator creating a NameId with the string supplied as value.
    /// </summary>
    /// <param name="value">The value of the NameId</param>
    public static implicit operator NameId(string value) => new(value);

    /// <summary>
    /// Creates a <see cref="NameId"/> from a string value.
    /// </summary>
    /// <param name="value">The value</param>
    /// <returns>A new NameId</returns>
    public static NameId FromString(string value) => new(value);

    /// <summary>
    /// Shows debugger friendly string
    /// </summary>
    /// <returns>string</returns>
    public override string ToString() => Value;

    /// <summary>
    /// Comparison
    /// </summary>
    /// <param name="other">Object to compare to</param>
    /// <returns>Are they equal?</returns>
    public override bool Equals(object? other) =>
        (other is NameId othernameid
        && othernameid.Value == Value && othernameid.Format == Format
        && othernameid.SPNameQualifier == SPNameQualifier && othernameid.NameQualifier == NameQualifier)
        ||
        (other is string otherstring
        && otherstring == Value && Format == null
        && SPNameQualifier == null && NameQualifier == null);

    /// <summary>
    /// Operator ==
    /// </summary>
    /// <param name="n1">Object</param>
    /// <param name="n2">Object to compare to</param>
    /// <returns>
    /// <c>true</c> if both instances are non-null and their Format and Value properties are equal.
    /// <c>true</c> if both instances are the same reference, otherwise returns <c>false</c>.
    /// </returns>
    public static bool operator ==(NameId? n1, NameId? n2)
    {
        if (ReferenceEquals(n1, n2))
        {
            return true;
        }

        if (n1 is null || n2 is null)
        {
            return false;
        }

        return n1.Value == n2.Value && n1.Format == n2.Format
            && n1.SPNameQualifier == n2.SPNameQualifier && n1.NameQualifier == n2.NameQualifier;
    }

    /// <summary>
    /// Operator !=
    /// </summary>
    /// <param name="n1">Object</param>
    /// <param name="n2">Object to compare to</param>
    /// <returns>
    /// <c>True</c> if the two instances are not equal, otherwise <c>false</c>
    /// </returns>
    public static bool operator !=(NameId? n1, NameId? n2) =>
        !(n1 == n2);

    /// <summary>
    /// Get hash code
    /// </summary>
    /// <returns>Hash code</returns>
    public override int GetHashCode() =>
        HashCode.Combine(Value, Format, SPNameQualifier, NameQualifier);
}
