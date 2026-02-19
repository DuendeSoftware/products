// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.ConformanceReport;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.ConformanceReport;

/// <summary>
/// Adapts IdentityServer Client to ConformanceReportClient.
/// </summary>
internal static class ClientAdapter
{
    public static ConformanceReportClient ToConformanceReportClient(this Client client) =>
        new()
        {
            ClientId = client.ClientId,
            ClientName = client.ClientName,
            AllowedGrantTypes = client.AllowedGrantTypes.ToList(),
            RequirePkce = client.RequirePkce,
            AllowPlainTextPkce = client.AllowPlainTextPkce,
            RedirectUris = client.RedirectUris.ToList(),
            RequireClientSecret = client.RequireClientSecret,
            ClientSecretTypes = client.ClientSecrets
                .Select(s => s.Type)
                .Distinct()
                .ToList(),
            RequirePushedAuthorization = client.RequirePushedAuthorization,
            RequireDPoP = client.RequireDPoP,
            DPoPValidationMode = MapDPoPMode(client.DPoPValidationMode),
            AuthorizationCodeLifetime = client.AuthorizationCodeLifetime,
            AllowOfflineAccess = client.AllowOfflineAccess,
            RefreshTokenUsage = MapTokenUsage(client.RefreshTokenUsage),
            AllowAccessTokensViaBrowser = client.AllowAccessTokensViaBrowser,
            RequireRequestObject = client.RequireRequestObject
        };

    private static ConformanceReportTokenUsage MapTokenUsage(TokenUsage usage) => usage switch
    {
        TokenUsage.OneTimeOnly => ConformanceReportTokenUsage.OneTimeOnly,
        _ => ConformanceReportTokenUsage.ReUse
    };

    private static ConformanceReportDPoPValidationMode MapDPoPMode(
        DPoPTokenExpirationValidationMode mode)
    {
        var result = ConformanceReportDPoPValidationMode.None;
        if (mode.HasFlag(DPoPTokenExpirationValidationMode.Nonce))
        {
            result |= ConformanceReportDPoPValidationMode.Nonce;
        }

        if (mode.HasFlag(DPoPTokenExpirationValidationMode.Iat))
        {
            result |= ConformanceReportDPoPValidationMode.Iat;
        }

        return result;
    }
}
