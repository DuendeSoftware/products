// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.Security.Claims;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml;
using Duende.IdentityServer.Saml.Models;
using Duende.IdentityServer.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Internal.Saml;

internal class SamlClaimsService(
    IProfileService profileService,
    ILogger<SamlClaimsService> logger,
    IOptions<SamlOptions> options,
    ISamlClaimsMapper? customMapper = null)
{
    private async Task<IEnumerable<Claim>> GetClaimsAsync(ClaimsPrincipal user, SamlServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var requestedClaimTypes = user.Claims.Select(c => c.Type).Distinct();

        // Use IdentityServer's IProfileService to get claims
        var context = new ProfileDataRequestContext
        {
            Subject = user,
            Client = new Client
            {
                ClientId = serviceProvider.EntityId.ToString()
            },
            RequestedClaimTypes = requestedClaimTypes,
            Caller = "SAML"
        };

        await profileService.GetProfileDataAsync(context);

        var claims = context.IssuedClaims;

        logger.RetrievedClaimsFromProfileService(LogLevel.Debug, claims.Count);

        return claims;
    }

    internal async Task<IEnumerable<SamlAttribute>> GetMappedAttributesAsync(
        ClaimsPrincipal user,
        SamlServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        var claims = await GetClaimsAsync(user, serviceProvider);

        if (customMapper != null)
        {
            logger.UsingCustomClaimMapper(LogLevel.Debug, serviceProvider.EntityId);
            var claimsMappingContext = new SamlClaimsMappingContext { UserClaims = claims, ServiceProvider = serviceProvider };
            return await customMapper.MapClaimsAsync(claimsMappingContext);
        }

        return MapClaimsToAttributes(claims, serviceProvider);
    }

    private List<SamlAttribute> MapClaimsToAttributes(
        IEnumerable<Claim> claims,
        SamlServiceProvider serviceProvider)
    {
        var samlOptions = options.Value;
        var attributes = new List<SamlAttribute>();
        var claimsList = claims.ToList();

        foreach (var claim in claimsList)
        {
            // Determine attribute name: SP mapping > Global mapping > null (exclude)
            var attributeName = GetAttributeName(claim.Type, serviceProvider, samlOptions);

            // Skip claims that aren't mapped
            if (attributeName == null)
            {
                continue;
            }

            // Check if attribute already exists (for multi-valued attributes)
            var existingAttr = attributes.FirstOrDefault(a => a.Name == attributeName);
            if (existingAttr != null)
            {
                existingAttr.Values.Add(claim.Value);
            }
            else
            {
                attributes.Add(new SamlAttribute
                {
                    Name = attributeName,
                    NameFormat = samlOptions.DefaultAttributeNameFormat,
                    FriendlyName = attributeName,
                    Values = [claim.Value]
                });
            }
        }

        logger.MappedClaimsToAttributes(LogLevel.Debug, claimsList.Count, attributes.Count, serviceProvider.EntityId);

        return attributes;
    }

    private static string? GetAttributeName(
        string claimType,
        SamlServiceProvider serviceProvider,
        SamlOptions options)
    {
        if (serviceProvider.ClaimMappings.TryGetValue(claimType, out var spMapping))
        {
            return spMapping;
        }

        if (options.DefaultClaimMappings.TryGetValue(claimType, out var globalMapping))
        {
            return globalMapping;
        }

        return null;
    }
}
