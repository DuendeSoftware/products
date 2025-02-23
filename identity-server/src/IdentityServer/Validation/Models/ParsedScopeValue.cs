// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Models a parsed scope value.
/// </summary>
public class ParsedScopeValue
{
    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="rawValue"></param>
    public ParsedScopeValue(string rawValue)
        : this(rawValue, rawValue, null)
    {
    }

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="rawValue"></param>
    /// <param name="parsedName"></param>
    /// <param name="parsedParameter"></param>
    public ParsedScopeValue(string rawValue, string parsedName, string? parsedParameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(parsedName);

        RawValue = rawValue;
        ParsedName = parsedName;
        ParsedParameter = parsedParameter;
    }

    /// <summary>
    /// The original (raw) value of the scope.
    /// </summary>
    public string RawValue { get; set; }

    /// <summary>
    /// The parsed name of the scope. If the scope has no structure, the parsed name will be the same as the raw value.
    /// </summary>
    public string ParsedName { get; set; }

    // future: maybe this should be something w/ more structure? dictionary?
    /// <summary>
    /// The parameter value of the parsed scope. If the scope has no structure, then the value will be null.
    /// </summary>
    public string? ParsedParameter { get; set; }
}
