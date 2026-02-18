// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Claims;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Internal.Saml.SingleSignin.Models;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Internal.Saml.SingleSignin;

internal class SamlNameIdGenerator(IOptions<SamlOptions> samlOptions, ILogger<SamlNameIdGenerator> logger)
{
    private readonly SamlOptions _samlOptions = samlOptions.Value;

    internal NameIdentifier GenerateNameIdentifier(
        ClaimsPrincipal user,
        SamlServiceProvider samlServiceProvider,
        AuthNRequest? request)
    {
        // Format selection priority: Request > SP Default > IdP Default
        var format = request?.NameIdPolicy?.Format
                     ?? samlServiceProvider.DefaultNameIdFormat
                     ?? SamlConstants.NameIdentifierFormats.Unspecified;

        logger.UsingNameIdFormat(LogLevel.Debug, format);

        var value = format switch
        {
            SamlConstants.NameIdentifierFormats.EmailAddress => GetEmailNameId(user),
            SamlConstants.NameIdentifierFormats.Persistent => GetPersistentNameId(samlServiceProvider, user),
            SamlConstants.NameIdentifierFormats.Transient => Guid.NewGuid().ToString(),
            _ => user.GetSubjectId()
        };

        var nameId = new NameIdentifier
        {
            Value = value,
            Format = format,
        };

        if (format == SamlConstants.NameIdentifierFormats.Persistent)
        {
            nameId.SPNameQualifier = samlServiceProvider.EntityId;
        }

        return nameId;
    }

    private static string GetEmailNameId(ClaimsPrincipal user)
    {
        // Try to get email claim
        var email = user.FindFirst("email")?.Value
                    ?? user.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
            throw new InvalidOperationException("Could not find email address for authenticated user");
        }

        return email;
    }

    private string GetPersistentNameId(SamlServiceProvider samlServiceProvider, ClaimsPrincipal user)
    {
        var persistentIdClaimType = samlServiceProvider.DefaultPersistentNameIdentifierClaimType ??
                                    _samlOptions.DefaultPersistentNameIdentifierClaimType;

        var persistentIdentifier = user.FindFirst(persistentIdClaimType);
        if (persistentIdentifier == null || string.IsNullOrEmpty(persistentIdentifier.Value))
        {
            throw new InvalidOperationException("Could not find persistent identifier for authenticated user");
        }

        return persistentIdentifier.Value;
    }
}
