// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Extensions;
using IdentityModel;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Additional checks applied to an <c>id_token_hint</c> at the end session endpoint, beyond the
/// signature, issuer, and lifetime checks performed by <see cref="ITokenValidator"/>. These checks
/// confirm the token presented as a hint has the shape of an identity token that this server
/// issued, rather than some other kind of token this server also issues (such as an access token,
/// a logout token, or a token-introspection response).
/// </summary>
internal static class IdentityTokenHintChecks
{
    private const string GenericJwtType = "JWT";
    private const string IntrospectionJwtResponseType = "token-introspection+jwt";

    internal const string NoSubClaimReason = "id_token_hint has no sub claim";
    internal const string UnparsableReason = "id_token_hint could not be parsed as a JWT";
    internal const string UnexpectedTypeReason = "id_token_hint has an unexpected token type";
    internal const string LogoutTokenShapeReason = "id_token_hint has the shape of a logout token";
    internal const string AccessTokenShapeReason = "id_token_hint has the shape of an access token";

    // Emitted at most once per process: the options rarely change at runtime, and warning on
    // every id_token_hint validation would be both noisy and wasteful.
    private static int s_reservedTypeWarningEmitted;

    /// <summary>
    /// Checks the already signature-validated <paramref name="result"/> against the shape
    /// expected of an identity token. The <c>typ</c> header is read from <paramref name="idTokenHint"/>
    /// (the token as submitted), not from <paramref name="result"/>, because a custom or
    /// decorating <see cref="ITokenValidator"/> may not populate <see cref="TokenValidationResult.Jwt"/>.
    /// Returns null when the checks pass, or a short, non-sensitive failure reason otherwise.
    /// </summary>
    public static string? Validate(string idTokenHint, TokenValidationResult result, IdentityServerOptions options, ILogger logger)
    {
        var claims = result.Claims?.ToArray() ?? [];

        var sub = claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Subject);
        if (sub == null || sub.Value.IsMissing())
        {
            logger.LogInformation("id_token_hint has no sub claim");
            return NoSubClaimReason;
        }

        if (!TryGetJwtType(idTokenHint, out var typ))
        {
            logger.LogInformation("id_token_hint could not be parsed as a JWT");
            return UnparsableReason;
        }

        if (typ.IsPresent())
        {
            var effectiveTypes = GetEffectiveIdentityTokenTypes(options, logger);
            if (!effectiveTypes.Contains(typ))
            {
                logger.LogInformation("id_token_hint has an unexpected typ header: {Type}", typ);
                return UnexpectedTypeReason;
            }
        }

        // The checks below only apply when the typ header is missing or the generic "JWT": a
        // recognized, non-generic identity token type (checked above) already rules out these
        // other kinds of tokens.
        if (typ.IsMissing() || string.Equals(typ, GenericJwtType, StringComparison.OrdinalIgnoreCase))
        {
            if (HasLogoutTokenShape(claims))
            {
                logger.LogInformation("id_token_hint has an events claim containing the back-channel logout event");
                return LogoutTokenShapeReason;
            }

            var hasClientId = claims.Any(c => c.Type == JwtClaimTypes.ClientId);
            var hasScope = claims.Any(c => c.Type == JwtClaimTypes.Scope);
            if (hasClientId && hasScope)
            {
                logger.LogInformation("id_token_hint carries both client_id and scope claims, the shape of an access token, and was rejected");
                return AccessTokenShapeReason;
            }
        }

        return null;
    }

    private static bool HasLogoutTokenShape(IReadOnlyCollection<Claim> claims)
    {
        var events = claims.FirstOrDefault(c => c.Type == JwtClaimTypes.Events);
        if (events == null || events.Value.IsMissing())
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(events.Value);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty(OidcConstants.Events.BackChannelLogout, out _);
        }
        catch (JsonException)
        {
            // Not a JSON object (e.g. a plain string value like "calendar"): not a logout-token shape.
            return false;
        }
    }

    private static bool TryGetJwtType(string jwt, out string? typ)
    {
        try
        {
            typ = new JsonWebToken(jwt).Typ;
            return true;
        }
        catch (Exception)
        {
            typ = null;
            return false;
        }
    }

    // "JWT" is the generic type and doesn't identify a kind of token, so it is never reserved,
    // even when AccessTokenJwtType or LogoutTokenJwtType is set to it for older consumers.
    //
    // Computed fresh per call: this runs once per end-session request with an id_token_hint, not
    // once per token-validation call, so recomputing is cheap and always reflects the current
    // options even if a test mutates them after the pipeline's Initialize.
    private static HashSet<string> GetEffectiveIdentityTokenTypes(IdentityServerOptions options, ILogger logger)
    {
        var reservedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (options.AccessTokenJwtType.IsPresent())
        {
            reservedTypes.Add(options.AccessTokenJwtType);
        }
        if (options.LogoutTokenJwtType.IsPresent())
        {
            reservedTypes.Add(options.LogoutTokenJwtType);
        }
        reservedTypes.Add(IntrospectionJwtResponseType);
        reservedTypes.Remove(GenericJwtType);

        var allowedTypes = options.IdentityTokenJwtTypes ?? [];
        var effectiveTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var allowedType in allowedTypes)
        {
            if (reservedTypes.Contains(allowedType))
            {
                if (Interlocked.Exchange(ref s_reservedTypeWarningEmitted, 1) == 0)
                {
                    logger.LogWarning("IdentityTokenJwtTypes contains '{Type}', which is reserved for another kind of token and will be ignored", allowedType);
                }

                continue;
            }

            effectiveTypes.Add(allowedType);
        }

        return effectiveTypes;
    }
}
