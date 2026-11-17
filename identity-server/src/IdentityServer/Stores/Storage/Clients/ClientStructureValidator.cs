// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores.Storage.Clients;

internal sealed class ClientStructureValidator(
    ValidationOptions validationOptions) : IConfigurationValidator<ClientConfiguration>
{
    public Task<IReadOnlyList<StorageError>> ValidateAsync(ClientConfiguration configuration, Ct ct)
    {
        var errors = new List<StorageError>();

        if (string.IsNullOrWhiteSpace(configuration.ClientId))
        {
            errors.Add(StorageError.Required(nameof(configuration.ClientId)));
        }

        if (configuration.ClientName is not null && string.IsNullOrWhiteSpace(configuration.ClientName))
        {
            errors.Add(StorageError.InvalidValue(nameof(configuration.ClientName),
                "Client name must not be empty or whitespace."));
        }

        ValidateGrantTypes(configuration, errors);
        ValidateScopes(configuration, errors);
        ValidateUriCollections(configuration, errors);
        ValidateSingleUris(configuration, errors);
        ValidateFrontChannelOrigin(configuration, errors);

        return Task.FromResult<IReadOnlyList<StorageError>>(errors);
    }

    private static void ValidateGrantTypes(ClientConfiguration configuration, List<StorageError> errors)
    {
        if (configuration.AllowedGrantTypes is null)
        {
            return;
        }

        foreach (var grantType in configuration.AllowedGrantTypes)
        {
            if (string.IsNullOrWhiteSpace(grantType))
            {
                errors.Add(StorageError.InvalidValue(nameof(configuration.AllowedGrantTypes),
                    "Grant type must not be null or whitespace."));
                return;
            }

            if (grantType.Contains(' ', StringComparison.Ordinal))
            {
                errors.Add(StorageError.InvalidValue(nameof(configuration.AllowedGrantTypes),
                    $"Grant type '{grantType}' contains spaces."));
                return;
            }
        }

        if (configuration.AllowedGrantTypes.Count !=
            configuration.AllowedGrantTypes.Distinct(StringComparer.Ordinal).Count())
        {
            errors.Add(StorageError.InvalidValue(nameof(configuration.AllowedGrantTypes),
                "Grant types list contains duplicate values."));
            return;
        }

        var grantTypes = configuration.AllowedGrantTypes;

        if (ContainsBoth(grantTypes, GrantType.Implicit, GrantType.AuthorizationCode))
        {
            errors.Add(StorageError.ValidationFailed(
                $"Grant types list cannot contain both {GrantType.Implicit} and {GrantType.AuthorizationCode}.",
                nameof(configuration.AllowedGrantTypes)));
        }

        if (ContainsBoth(grantTypes, GrantType.Implicit, GrantType.Hybrid))
        {
            errors.Add(StorageError.ValidationFailed(
                $"Grant types list cannot contain both {GrantType.Implicit} and {GrantType.Hybrid}.",
                nameof(configuration.AllowedGrantTypes)));
        }

        if (ContainsBoth(grantTypes, GrantType.AuthorizationCode, GrantType.Hybrid))
        {
            errors.Add(StorageError.ValidationFailed(
                $"Grant types list cannot contain both {GrantType.AuthorizationCode} and {GrantType.Hybrid}.",
                nameof(configuration.AllowedGrantTypes)));
        }
    }

    private static void ValidateScopes(ClientConfiguration configuration, List<StorageError> errors)
    {
        if (configuration.AllowedScopes is null)
        {
            return;
        }

        foreach (var scope in configuration.AllowedScopes)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                errors.Add(StorageError.InvalidValue(nameof(configuration.AllowedScopes),
                    "Scope must not be null or whitespace."));
                return;
            }
        }

        var names = configuration.AllowedScopes.Distinct(StringComparer.Ordinal).ToList();
        if (names.Count == 0)
        {
            return;
        }

        if (names.Contains(IdentityServerConstants.StandardScopes.OfflineAccess, StringComparer.Ordinal))
        {
            errors.Add(StorageError.InvalidValue(nameof(configuration.AllowedScopes),
                $"'{IdentityServerConstants.StandardScopes.OfflineAccess}' is not a resource; use '{nameof(configuration.AllowOfflineAccess)}' instead."));
        }

        // Scope-name existence is resolved once, at save time, by ClientAdmin.ResolveAllowedScopesAsync;
        // it builds the same storage link targets this validator would otherwise have to look up again.
    }

    private void ValidateUriCollections(ClientConfiguration configuration, List<StorageError> errors)
    {
        ValidateCorsOrigins(configuration, errors);
        ValidateRedirectUris(configuration, errors);
        ValidatePostLogoutRedirectUris(configuration, errors);
    }

    private static void ValidateCorsOrigins(ClientConfiguration configuration, List<StorageError> errors)
    {
        if (configuration.AllowedCorsOrigins is null)
        {
            return;
        }

        foreach (var origin in configuration.AllowedCorsOrigins)
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                errors.Add(StorageError.InvalidValue(nameof(configuration.AllowedCorsOrigins),
                    "CORS origin must not be null or whitespace."));
                return;
            }

            var reason = ClientUriRules.ValidateCorsOrigin(origin);
            if (reason is not null)
            {
                errors.Add(StorageError.InvalidValue(nameof(configuration.AllowedCorsOrigins),
                    $"CORS origin '{origin}' {reason}."));
                return;
            }
        }
    }

    private void ValidateRedirectUris(ClientConfiguration configuration, List<StorageError> errors)
    {
        if (configuration.RedirectUris is null)
        {
            return;
        }

        foreach (var uri in configuration.RedirectUris)
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                errors.Add(StorageError.InvalidValue(nameof(configuration.RedirectUris),
                    "Redirect URI must not be null or whitespace."));
                return;
            }

            var reason = ClientUriRules.ValidateRedirectUri(uri, validationOptions.InvalidRedirectUriPrefixes);
            if (reason is not null)
            {
                errors.Add(StorageError.InvalidValue(nameof(configuration.RedirectUris),
                    $"Redirect URI '{uri}' {reason}."));
                return;
            }
        }
    }

    private void ValidatePostLogoutRedirectUris(ClientConfiguration configuration, List<StorageError> errors)
    {
        if (configuration.PostLogoutRedirectUris is null)
        {
            return;
        }

        foreach (var uri in configuration.PostLogoutRedirectUris)
        {
            if (string.IsNullOrWhiteSpace(uri))
            {
                errors.Add(StorageError.InvalidValue(nameof(configuration.PostLogoutRedirectUris),
                    "Post-logout redirect URI must not be null or whitespace."));
                return;
            }

            var reason = ClientUriRules.ValidateRedirectUri(uri, validationOptions.InvalidRedirectUriPrefixes);
            if (reason is not null)
            {
                errors.Add(StorageError.InvalidValue(nameof(configuration.PostLogoutRedirectUris),
                    $"Post-logout redirect URI '{uri}' {reason}."));
                return;
            }
        }
    }

    private static void ValidateSingleUris(ClientConfiguration configuration, List<StorageError> errors)
    {
        ValidateSingleWebUri(nameof(configuration.ClientUri), "Client URI", configuration.ClientUri, errors);
        ValidateSingleWebUri(nameof(configuration.LogoUri), "Logo URI", configuration.LogoUri, errors);
        ValidateSingleWebUri(nameof(configuration.FrontChannelLogoutUri), "Front-channel logout URI",
            configuration.FrontChannelLogoutUri, errors);
        ValidateSingleWebUri(nameof(configuration.InitiateLoginUri), "Initiate login URI",
            configuration.InitiateLoginUri, errors);

        if (string.IsNullOrEmpty(configuration.BackChannelLogoutUri))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(configuration.BackChannelLogoutUri))
        {
            errors.Add(StorageError.InvalidValue(nameof(configuration.BackChannelLogoutUri),
                "Back-channel logout URI must not be empty or whitespace."));
            return;
        }

        var reason = ClientUriRules.ValidateBackChannelUri(configuration.BackChannelLogoutUri);
        if (reason is not null)
        {
            errors.Add(StorageError.InvalidValue(nameof(configuration.BackChannelLogoutUri),
                $"Back-channel logout URI '{configuration.BackChannelLogoutUri}' {reason}."));
        }
    }

    private static void ValidateSingleWebUri(string propertyName, string label, string value, List<StorageError> errors)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(StorageError.InvalidValue(propertyName, $"{label} must not be empty or whitespace."));
            return;
        }

        var reason = ClientUriRules.ValidateWebUrl(value);
        if (reason is not null)
        {
            errors.Add(StorageError.InvalidValue(propertyName, $"{label} '{value}' {reason}."));
        }
    }

    private static void ValidateFrontChannelOrigin(ClientConfiguration configuration, List<StorageError> errors)
    {
        var frontChannelLogoutUri = configuration.FrontChannelLogoutUri;

        if (string.IsNullOrEmpty(frontChannelLogoutUri))
        {
            return;
        }

        if (errors.Any(e => e.PropertyNames?.Contains(nameof(configuration.FrontChannelLogoutUri)) == true))
        {
            return;
        }

        if (configuration.RedirectUris is null || configuration.RedirectUris.Count == 0)
        {
            return;
        }

        if (!Uri.TryCreate(frontChannelLogoutUri, UriKind.Absolute, out var frontChannelUri))
        {
            return;
        }

        var hasMatch = false;
        foreach (var redirectUri in configuration.RedirectUris)
        {
            if (Uri.TryCreate(redirectUri, UriKind.Absolute, out var parsedRedirectUri) &&
                ClientUriRules.HaveSameOrigin(frontChannelUri, parsedRedirectUri))
            {
                hasMatch = true;
                break;
            }
        }

        if (!hasMatch)
        {
            errors.Add(StorageError.InvalidValue(nameof(configuration.FrontChannelLogoutUri),
                $"Front-channel logout URI '{frontChannelLogoutUri}' must have the same scheme, host and port as one of the client's redirect URIs."));
        }
    }

    private static bool ContainsBoth(IEnumerable<string> grantTypes, string a, string b) =>
        grantTypes.Contains(a, StringComparer.Ordinal) && grantTypes.Contains(b, StringComparer.Ordinal);
}
