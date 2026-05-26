// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#pragma warning disable CA1056

namespace Duende.UserManagement.Internal.Services;

/// <summary>
/// Configures the per-request URLs and paths into the current server.
/// </summary>
internal interface IServerUrls
{
    /// <summary>
    /// Gets or sets the origin for the server. For example, "https://server.acme.com:5001".
    /// </summary>
    string Origin { get; set; }

    /// <summary>
    /// Gets or sets the base path of the server.
    /// </summary>
    string? BasePath { get; set; }

    /// <summary>
    /// Gets the base URL for the server.
    /// </summary>
    string BaseUrl => Origin + BasePath;
}
