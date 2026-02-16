// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace Duende.IdentityServer.Models;

/// <summary>
/// Extension methods for client.
/// </summary>
public static class ClientExtensions
{
    /// <summary>
    /// Returns true if the client is an implicit-only client.
    /// </summary>
    public static bool IsImplicitOnly(this Client client) => client != null &&
               client.AllowedGrantTypes != null &&
               client.AllowedGrantTypes.Count == 1 &&
               client.AllowedGrantTypes.First() == GrantType.Implicit;

    /// <summary>
    /// Constructs a list of SecurityKey from a Secret collection
    /// </summary>
    /// <param name="secrets">The secrets</param>
    /// <returns></returns>
    public static Task<List<SecurityKey>> GetKeysAsync(this IEnumerable<Secret> secrets)
    {
        var secretList = secrets.ToList().AsReadOnly();
        var keys = new List<SecurityKey>();

        var certificates = GetCertificates(secretList)
            .Select(c => (SecurityKey)new X509SecurityKey(c))
            .ToList();
        keys.AddRange(certificates);

        var jwks = secretList
            .Where(s => s.Type == IdentityServerConstants.SecretTypes.JsonWebKey)
            .Select(s => new Microsoft.IdentityModel.Tokens.JsonWebKey(s.Value))
            .ToList();
        keys.AddRange(jwks);

        return Task.FromResult(keys);
    }

    private static List<X509Certificate2> GetCertificates(IEnumerable<Secret> secrets) =>
#pragma warning disable SYSLIB0057 // Type or member is obsolete
        // TODO - Use X509CertificateLoader in a future release (when we drop NET8 support)
        secrets
            .Where(s => s.Type == IdentityServerConstants.SecretTypes.X509CertificateBase64)
            .Select(s =>
                new X509Certificate2(Convert.FromBase64String(s.Value)))
            .Where(c => c != null)
            .ToList();
#pragma warning restore SYSLIB0057 // Type or member is obsolete

}
