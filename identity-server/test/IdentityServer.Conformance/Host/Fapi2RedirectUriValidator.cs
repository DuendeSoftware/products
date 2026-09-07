// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;

namespace Duende.IdentityServer.Conformance.Host;

/// <summary>
/// A redirect URI validator that matches the base URI (scheme, host, port, path)
/// while allowing additional query parameters. This is needed for FAPI 2.0
/// conformance testing where the conformance suite appends query parameters
/// (e.g., ?dummy1=lorem&amp;dummy2=ipsum) to the registered redirect URI.
/// </summary>
internal sealed class Fapi2RedirectUriValidator : IRedirectUriValidator
{
    /// <inheritdoc/>
    [Obsolete("Deprecated overload")]
    public Task<bool> IsRedirectUriValidAsync(string requestedUri, Client client) =>
        Task.FromResult(IsValid(requestedUri, client.RedirectUris));

    /// <inheritdoc/>
    public Task<bool> IsRedirectUriValidAsync(RedirectUriValidationContext context, Ct ct) =>
        Task.FromResult(IsValid(context.RequestedUri, context.Client.RedirectUris));

    /// <inheritdoc/>
    public Task<bool> IsPostLogoutRedirectUriValidAsync(string requestedUri, Client client, Ct ct) =>
        Task.FromResult(IsValid(requestedUri, client.PostLogoutRedirectUris));

    private static bool IsValid(string requestedUri, ICollection<string> registeredUris)
    {
        if (!Uri.TryCreate(requestedUri, UriKind.Absolute, out var requestedParsed))
        {
            return false;
        }

        foreach (var registered in registeredUris)
        {
            if (!Uri.TryCreate(registered, UriKind.Absolute, out var registeredParsed))
            {
                continue;
            }

            // Match scheme, host, port, and path exactly.
            // Allow any query parameters on the requested URI as long as the base matches.
            if (string.Equals(requestedParsed.Scheme, registeredParsed.Scheme, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(requestedParsed.Host, registeredParsed.Host, StringComparison.OrdinalIgnoreCase) &&
                requestedParsed.Port == registeredParsed.Port &&
                string.Equals(requestedParsed.AbsolutePath, registeredParsed.AbsolutePath, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
