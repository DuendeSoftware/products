// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Internal.Saml.SingleSignin.Models;

/// <summary>
/// Represents the status of a SAML Response.
/// </summary>
internal record Status
{
    /// <summary>
    /// Gets or sets the status code indicating the success or failure of the request.
    /// </summary>
    public required string StatusCode { get; set; }

    /// <summary>
    /// Gets or sets an optional human-readable message providing additional information about the status.
    /// </summary>
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Gets or sets an optional nested status code for more detailed error information.
    /// </summary>
    public string? NestedStatusCode { get; set; }
}
