// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Admin;
using Duende.IdentityServer.Admin.Clients;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Stores.Storage.Clients;

internal class ClientStructureValidator : IConfigurationValidator<ClientConfiguration>
{
    public Task<IReadOnlyList<AdminError>> ValidateAsync(ClientConfiguration configuration, Ct ct)
    {
        var errors = new List<AdminError>();

        if (string.IsNullOrWhiteSpace(configuration.ClientId))
        {
            errors.Add(AdminError.Required(nameof(configuration.ClientId)));
        }

        if (configuration.ClientName is not null && string.IsNullOrWhiteSpace(configuration.ClientName))
        {
            errors.Add(AdminError.InvalidValue(nameof(configuration.ClientName),
                "Client name must not be empty or whitespace."));
        }

        ValidateGrantTypes(configuration, errors);
        ValidateScopes(configuration, errors);
        ValidateUriCollections(configuration, errors);

        return Task.FromResult<IReadOnlyList<AdminError>>(errors);
    }

    private static void ValidateGrantTypes(ClientConfiguration configuration, List<AdminError> errors)
    {
        if (configuration.AllowedGrantTypes is null)
        {
            return;
        }

        foreach (var grantType in configuration.AllowedGrantTypes)
        {
            if (string.IsNullOrWhiteSpace(grantType))
            {
                errors.Add(AdminError.InvalidValue(nameof(configuration.AllowedGrantTypes),
                    "Grant type must not be null or whitespace."));
                return;
            }

            if (grantType.Contains(' ', StringComparison.Ordinal))
            {
                errors.Add(AdminError.InvalidValue(nameof(configuration.AllowedGrantTypes),
                    $"Grant type '{grantType}' contains spaces."));
                return;
            }
        }

        if (configuration.AllowedGrantTypes.Count !=
            configuration.AllowedGrantTypes.Distinct(StringComparer.Ordinal).Count())
        {
            errors.Add(AdminError.InvalidValue(nameof(configuration.AllowedGrantTypes),
                "Grant types list contains duplicate values."));
            return;
        }

        var grantTypes = configuration.AllowedGrantTypes;

        if (ContainsBoth(grantTypes, GrantType.Implicit, GrantType.AuthorizationCode))
        {
            errors.Add(AdminError.ValidationFailed(
                $"Grant types list cannot contain both {GrantType.Implicit} and {GrantType.AuthorizationCode}.",
                nameof(configuration.AllowedGrantTypes)));
        }

        if (ContainsBoth(grantTypes, GrantType.Implicit, GrantType.Hybrid))
        {
            errors.Add(AdminError.ValidationFailed(
                $"Grant types list cannot contain both {GrantType.Implicit} and {GrantType.Hybrid}.",
                nameof(configuration.AllowedGrantTypes)));
        }

        if (ContainsBoth(grantTypes, GrantType.AuthorizationCode, GrantType.Hybrid))
        {
            errors.Add(AdminError.ValidationFailed(
                $"Grant types list cannot contain both {GrantType.AuthorizationCode} and {GrantType.Hybrid}.",
                nameof(configuration.AllowedGrantTypes)));
        }
    }

    private static void ValidateScopes(ClientConfiguration configuration, List<AdminError> errors)
    {
        if (configuration.AllowedScopes is not null)
        {
            foreach (var scope in configuration.AllowedScopes)
            {
                if (string.IsNullOrWhiteSpace(scope))
                {
                    errors.Add(AdminError.InvalidValue(nameof(configuration.AllowedScopes),
                        "Scope must not be null or whitespace."));
                    return;
                }
            }
        }
    }

    private static void ValidateUriCollections(ClientConfiguration configuration, List<AdminError> errors)
    {
        if (configuration.AllowedCorsOrigins is not null)
        {
            foreach (var origin in configuration.AllowedCorsOrigins)
            {
                if (string.IsNullOrWhiteSpace(origin))
                {
                    errors.Add(AdminError.InvalidValue(nameof(configuration.AllowedCorsOrigins),
                        "CORS origin must not be null or whitespace."));
                    return;
                }
            }
        }

        if (configuration.RedirectUris is not null)
        {
            foreach (var uri in configuration.RedirectUris)
            {
                if (string.IsNullOrWhiteSpace(uri))
                {
                    errors.Add(AdminError.InvalidValue(nameof(configuration.RedirectUris),
                        "Redirect URI must not be null or whitespace."));
                    return;
                }
            }
        }

        if (configuration.PostLogoutRedirectUris is not null)
        {
            foreach (var uri in configuration.PostLogoutRedirectUris)
            {
                if (string.IsNullOrWhiteSpace(uri))
                {
                    errors.Add(AdminError.InvalidValue(nameof(configuration.PostLogoutRedirectUris),
                        "Post-logout redirect URI must not be null or whitespace."));
                    return;
                }
            }
        }
    }

    private static bool ContainsBoth(IEnumerable<string> grantTypes, string a, string b) =>
        grantTypes.Contains(a, StringComparer.Ordinal) && grantTypes.Contains(b, StringComparer.Ordinal);
}
