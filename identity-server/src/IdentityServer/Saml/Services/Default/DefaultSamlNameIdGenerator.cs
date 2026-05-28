// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Saml.Models;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Saml.Services.Default;

/// <summary>
/// Default implementation of <see cref="ISamlNameIdGenerator"/> supporting email
/// and unspecified NameID formats.
/// </summary>
public sealed class DefaultSamlNameIdGenerator(
    IOptions<IdentityServerOptions> identityServerOptions) : ISamlNameIdGenerator
{
    /// <inheritdoc/>
    public Task<NameIdGenerationResult> GenerateAsync(NameIdGenerationContext context, Ct ct)
    {
        var result = context.ResolvedFormat switch
        {
            SamlConstants.NameIdentifierFormats.EmailAddress => GenerateEmail(context),
            _ => GenerateUnspecified(context)
        };

        return Task.FromResult(result);
    }

    private static NameIdGenerationResult GenerateUnspecified(NameIdGenerationContext context)
    {
        var sub = context.Subject.FindFirstValue(JwtClaimTypes.Subject);
        if (string.IsNullOrWhiteSpace(sub))
        {
            return NameIdGenerationResult.Failure(
                SamlStatusCodes.Responder,
                SamlStatusCodes.InvalidNameIdPolicy,
                "Subject identifier (sub) claim is missing or empty.");
        }

        return NameIdGenerationResult.Success(new NameId
        {
            Value = sub,
            Format = context.ResolvedFormat
        });
    }

    private NameIdGenerationResult GenerateEmail(NameIdGenerationContext context)
    {
        var claimType = context.ServiceProvider.EmailNameIdClaimType
            ?? identityServerOptions.Value.Saml.EmailNameIdClaimType;

        var email = context.Subject.FindFirstValue(claimType);

        if (string.IsNullOrWhiteSpace(email))
        {
            return NameIdGenerationResult.Failure(
                SamlStatusCodes.Responder,
                SamlStatusCodes.InvalidNameIdPolicy,
                "Email claim is required for email NameID format but was not found.");
        }

        return NameIdGenerationResult.Success(new NameId
        {
            Value = email,
            Format = SamlConstants.NameIdentifierFormats.EmailAddress
        });
    }
}
