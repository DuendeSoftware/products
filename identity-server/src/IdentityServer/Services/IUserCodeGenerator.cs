// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

namespace Duende.IdentityServer.Services;

/// <summary>
/// Implements device flow user code generation
/// </summary>
public interface IUserCodeGenerator
{
    /// <summary>
    /// Gets the type of the user code.
    /// </summary>
    /// <value>
    /// The type of the user code.
    /// </value>
    string UserCodeType { get; }

    /// <summary>
    /// Gets the retry limit.
    /// </summary>
    /// <value>
    /// The retry limit for getting a unique value.
    /// </value>
    int RetryLimit { get; }

    /// <summary>
    /// Generates the user code.
    /// </summary>
    Task<string> GenerateAsync();
}
