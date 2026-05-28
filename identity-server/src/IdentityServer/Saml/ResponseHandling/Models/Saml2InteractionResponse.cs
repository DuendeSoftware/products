// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

namespace Duende.IdentityServer.Saml.ResponseHandling;

/// <summary>
/// Represents the result of processing a SAML AuthnRequest interaction check.
/// </summary>
public sealed record Saml2InteractionResponse
{
    private Saml2InteractionResponse() { }

    /// <summary>
    /// Gets a value indicating whether the user must log in.
    /// </summary>
    public bool IsLogin { get; private init; }

    /// <summary>
    /// Gets a value indicating whether this is an error response.
    /// </summary>
    public bool IsError { get; private init; }

    /// <summary>
    /// Gets the top-level SAML status code (e.g., <c>urn:oasis:names:tc:SAML:2.0:status:Responder</c>).
    /// Only set when <see cref="IsError"/> is <see langword="true"/>.
    /// </summary>
    public string? StatusCode { get; private init; }

    /// <summary>
    /// Gets the nested SAML sub-status code (e.g., <c>urn:oasis:names:tc:SAML:2.0:status:NoPassive</c>).
    /// Only set when <see cref="IsError"/> is <see langword="true"/>.
    /// </summary>
    public string? SubStatusCode { get; private init; }

    /// <summary>
    /// Gets the human-readable error message.
    /// Only set when <see cref="IsError"/> is <see langword="true"/>.
    /// </summary>
    public string? Message { get; private init; }

    /// <summary>
    /// Creates a response indicating the user must log in.
    /// </summary>
    public static Saml2InteractionResponse Login() => new() { IsLogin = true };

    /// <summary>
    /// Creates a response indicating no interaction is required.
    /// </summary>
    public static Saml2InteractionResponse NoInteraction() => new();

    /// <summary>
    /// Creates an error response with SAML status and sub-status codes.
    /// </summary>
    /// <param name="statusCode">The top-level SAML status code.</param>
    /// <param name="subStatusCode">The nested SAML sub-status code.</param>
    public static Saml2InteractionResponse Error(string statusCode, string subStatusCode) =>
        new() { IsError = true, StatusCode = statusCode, SubStatusCode = subStatusCode };

    /// <summary>
    /// Creates an error response with SAML status codes and a human-readable message.
    /// </summary>
    /// <param name="statusCode">The top-level SAML status code.</param>
    /// <param name="subStatusCode">The nested SAML sub-status code.</param>
    /// <param name="message">A human-readable error message.</param>
    public static Saml2InteractionResponse Error(string statusCode, string subStatusCode, string message) =>
        new() { IsError = true, StatusCode = statusCode, SubStatusCode = subStatusCode, Message = message };
}
