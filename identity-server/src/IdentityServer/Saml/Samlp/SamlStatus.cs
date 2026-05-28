// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Saml.Samlp;

/// <summary>
/// Samlp Status element
/// </summary>
public class SamlStatus
{
    /// <summary>
    /// Status Code
    /// </summary>
    public StatusCode StatusCode { get; set; } = default!;

    /// <summary>
    /// Optional human-readable status message.
    /// </summary>
    public string? StatusMessage { get; set; }
}

/// <summary>
/// Samlp StatusCode element
/// </summary>
public class StatusCode
{
    /// <summary>
    /// Status code value.
    /// </summary>
    public string Value { get; set; } = default!;

    public StatusCode? NestedStatusCode { get; set; }
}
