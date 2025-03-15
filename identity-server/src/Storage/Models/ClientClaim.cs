// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Security.Claims;

namespace Duende.IdentityServer.Models;

/// <summary>
/// A client claim
/// </summary>
public class ClientClaim
{
    /// <summary>
    /// The claim type
    /// </summary>
    public string Type { get; set; } = default!;

    /// <summary>
    /// The claim value
    /// </summary>
    public string Value { get; set; } = default!;

    /// <summary>
    /// The claim value type
    /// </summary>
    public string ValueType { get; set; } = ClaimValueTypes.String;

    /// <summary>
    /// ctor
    /// </summary>
    public ClientClaim()
    {
    }

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="type"></param>
    /// <param name="value"></param>
    public ClientClaim(string type, string value)
    {
        Type = type;
        Value = value;
    }

    /// <summary>
    /// ctor
    /// </summary>
    /// <param name="type"></param>
    /// <param name="value"></param>
    /// <param name="valueType"></param>
    public ClientClaim(string type, string value, string valueType)
    {
        Type = type;
        Value = value;
        ValueType = valueType;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;

            hash = hash * 23 + Value.GetHashCode();
            hash = hash * 23 + Type.GetHashCode();
            hash = hash * 23 + ValueType.GetHashCode();
            return hash;
        }
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (obj is ClientClaim c)
        {
            return (string.Equals(Type, c.Type, StringComparison.Ordinal) &&
                    string.Equals(Value, c.Value, StringComparison.Ordinal) &&
                    string.Equals(ValueType, c.ValueType, StringComparison.Ordinal));
        }

        return false;
    }
}
