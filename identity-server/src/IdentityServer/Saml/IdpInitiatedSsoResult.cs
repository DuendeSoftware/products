// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Saml;

/// <summary>
/// The result of an IdP-initiated SSO operation. Either a success containing an
/// <see cref="IResult"/> that writes the SAML response via the appropriate
/// binding, or an error with a descriptive message the host can display in its portal UI.
/// </summary>
public sealed class IdpInitiatedSsoResult
{
    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsError { get; }

    /// <summary>
    /// Gets the error description when <see cref="IsError"/> is <c>true</c>.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets the entity ID of the target service provider, when available.
    /// </summary>
    public string? SpEntityId { get; }

    /// <summary>
    /// Gets the <see cref="IResult"/> that writes the SAML response to the browser
    /// via the appropriate binding (e.g., HTTP-POST auto-submit form). Return it from
    /// a Razor Page handler or minimal API endpoint. <c>null</c> when <see cref="IsError"/>
    /// is <c>true</c>.
    /// </summary>
    public IResult? Response { get; }

    private IdpInitiatedSsoResult(bool isError, string? error, string? spEntityId, IResult? response)
    {
        IsError = isError;
        Error = error;
        SpEntityId = spEntityId;
        Response = response;
    }

    /// <summary>
    /// Creates a successful result containing the SAML response.
    /// </summary>
    /// <param name="response">The <see cref="IResult"/> that writes the SAML response.
    /// Custom implementations are responsible for ensuring the result produces a properly
    /// signed SAML response via the appropriate binding.</param>
    /// <param name="spEntityId">The entity ID of the target service provider.</param>
    public static IdpInitiatedSsoResult Success(IResult response, string spEntityId)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(spEntityId);
        return new(false, null, spEntityId, response);
    }

    /// <summary>
    /// Creates a failure result with an error description.
    /// </summary>
    /// <param name="error">A description of why the operation failed.</param>
    /// <param name="spEntityId">The entity ID of the target service provider, if known.</param>
    public static IdpInitiatedSsoResult Failure(string error, string? spEntityId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new(true, error, spEntityId, null);
    }

    /// <summary>
    /// Creates a failure result with an error description, without a known SP entity ID.
    /// </summary>
    /// <param name="error">A description of why the operation failed.</param>
    public static IdpInitiatedSsoResult Failure(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new(true, error, null, null);
    }
}
