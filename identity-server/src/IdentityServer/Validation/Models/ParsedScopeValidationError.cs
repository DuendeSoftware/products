// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Models an error parsing a scope.
/// </summary>
public class ParsedScopeValidationError
{
    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="rawValue"></param>
    /// <param name="error"></param>
    public ParsedScopeValidationError(string rawValue, string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawValue);
        ArgumentException.ThrowIfNullOrWhiteSpace(error);

        RawValue = rawValue;
        Error = error;
    }

    /// <summary>
    /// The original (raw) value of the scope.
    /// </summary>
    public string RawValue { get; set; }

    /// <summary>
    /// Error message describing why the raw scope failed to be parsed.
    /// </summary>
    public string Error { get; set; }
}