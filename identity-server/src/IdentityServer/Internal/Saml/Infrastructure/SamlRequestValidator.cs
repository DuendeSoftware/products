// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Saml.Models;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.Internal.Saml.Infrastructure;

/// <summary>
/// Helper class for common SAML request validation logic
/// </summary>
internal class SamlRequestValidator(TimeProvider timeProvider, IOptions<SamlOptions> options)
{
    private readonly SamlOptions _samlOptions = options.Value;

    /// <summary>
    /// Validates version, issue instant, and destination for a SAML request
    /// </summary>
    internal SamlValidationError? ValidateCommonFields(
        SamlVersion version,
        DateTime issueInstant,
        Uri? destination,
        SamlServiceProvider serviceProvider,
        string expectedDestination)
    {
        // Version validation
        if (version != SamlVersion.V2)
        {
            return new SamlValidationError
            {
                Message = "Only Version 2.0 is supported",
                StatusCode = SamlStatusCode.VersionMismatch
            };
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var clockSkew = serviceProvider.ClockSkew ?? _samlOptions.DefaultClockSkew;

        // Issue instant not in future
        if (issueInstant > now.Add(clockSkew))
        {
            return new SamlValidationError
            {
                StatusCode = SamlStatusCode.Requester,
                Message = "Request IssueInstant is in the future"
            };
        }

        // Issue instant not too old
        var maxAge = serviceProvider.RequestMaxAge ?? _samlOptions.DefaultRequestMaxAge;
        if (issueInstant < now.Subtract(maxAge))
        {
            return new SamlValidationError
            {
                StatusCode = SamlStatusCode.Requester,
                Message = "Request has expired (IssueInstant too old)"
            };
        }

        // Destination validation
        if (destination != null)
        {
            if (!destination.ToString().Equals(expectedDestination, StringComparison.OrdinalIgnoreCase))
            {
                return new SamlValidationError
                {
                    StatusCode = SamlStatusCode.Requester,
                    Message = $"Invalid destination. Expected '{expectedDestination}'"
                };
            }
        }

        return null;
    }
}

/// <summary>
/// Represents a SAML validation error
/// </summary>
internal class SamlValidationError
{
    internal required string Message { get; init; }
    internal SamlStatusCode StatusCode { get; init; } = SamlStatusCode.Requester;
    internal SamlStatusCode? SubStatusCode { get; init; }
}
