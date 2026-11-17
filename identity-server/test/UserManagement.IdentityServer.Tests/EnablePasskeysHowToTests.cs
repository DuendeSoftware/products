// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Duende.IdentityServer;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Passkeys;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace IdentityServer8.UnitTests;

public sealed class EnablePasskeysHowToTests
{
    private static Ct Ct => TestContext.Current.CancellationToken;

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void ConfigureOptions(WebApplicationBuilder builder)
    {
        // begin-snippet: passkeys-configure-options
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.Authentication(auth => auth.Configure(o =>
            {
                o.Passkeys.ServerDomain = "example.com";
                o.Passkeys.AllowedOrigins = ["https://example.com"];
            })));
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Configured_passkey_options_are_applied()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        ConfigureOptions(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var options = app.Services.GetRequiredService<IOptions<UserAuthenticationOptions>>().Value;
        options.Passkeys.ServerDomain.ShouldBe("example.com");
        options.Passkeys.AllowedOrigins.ShouldBe(["https://example.com"]);
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void MapEndpoints(WebApplication app)
    {
        // begin-snippet: passkeys-map-endpoints
        _ = app.MapUserManagement();
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Mapped_endpoints_answer_at_the_default_routes()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        ConfigureOptions(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        MapEndpoints(app);

        await app.StartAsync(Ct);
        using var client = app.GetTestClient();

        // Unauthenticated, so IdentityServer's cookie handler redirects to login (302), not a 404:
        // the endpoint exists and is reachable.
        var registerBegin = await client.PostAsync("/passkeys/register/begin", null, Ct);
        registerBegin.StatusCode.ShouldBe(HttpStatusCode.Found);

        var authenticateBegin = await client.PostAsync("/passkeys/authenticate/discoverable/begin", null, Ct);
        authenticateBegin.StatusCode.ShouldBe(HttpStatusCode.OK);

        var passkeysJs = await client.GetAsync("/passkeys/js", Ct);
        _ = passkeysJs.EnsureSuccessStatusCode();
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterWithCustomRoutes(WebApplicationBuilder builder)
    {
        // begin-snippet: passkeys-custom-routes
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.Authentication(auth =>
                auth.ConfigureEndpoints(o => o.Passkeys.Route = "/my-passkeys")));
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Custom_route_replaces_the_default_route()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterWithCustomRoutes(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);
        MapEndpoints(app);

        await app.StartAsync(Ct);
        using var client = app.GetTestClient();

        var defaultRoute = await client.PostAsync("/passkeys/authenticate/discoverable/begin", null, Ct);
        defaultRoute.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var customRoute = await client.PostAsync("/my-passkeys/authenticate/discoverable/begin", null, Ct);
        customRoute.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterCustomSignatureVerifier(WebApplicationBuilder builder)
    {
        // begin-snippet: passkeys-add-signature-verifier
        _ = builder.Services.AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.Authentication(
                auth => auth.AddPasskeySignatureVerifier<Ed25519SignatureVerifier>()));
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Custom_signature_verifier_is_registered_alongside_the_built_in_verifiers()
    {
        var builder = WebApplication.CreateBuilder();
        _ = builder.WebHost.UseTestServer();
        RegisterCustomSignatureVerifier(builder);

        await using var app = builder.Build();
        await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

        var verifiers = app.Services.GetServices<ISignatureVerifier>();
        verifiers.ShouldContain(v => v is Ed25519SignatureVerifier);
    }

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static void RegisterAttestationTrustPolicy(WebApplicationBuilder builder, string vendorRootsPemPath, Guid knownAaguid)
    {
        // begin-snippet: passkeys-attestation-trust-policy
        var vendorRoots = new X509Certificate2Collection();
        vendorRoots.ImportFromPemFile(vendorRootsPemPath);

        _ = builder.Services
            // In production, build this map from FIDO MDS metadata or vendor-supplied root
            // certificates, one entry per AAGUID the authenticator is trusted to vouch for.
            .AddSingleton(new TrustedAuthenticatorRoots(new Dictionary<Guid, X509Certificate2Collection>
            {
                [knownAaguid] = vendorRoots
            }))
            .AddIdentityServer()
            .AddStorage(storage => storage.AddSqliteInMemory())
            .AddUserManagement(users => users.Authentication(
                auth => auth.AddAttestationTrustPolicy<KnownAaguidTrustPolicy>()));
        // end-snippet
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Custom_attestation_trust_policy_is_registered_and_uses_its_configured_roots()
    {
        var aaguid = Guid.NewGuid();
        var (leaf, root) = CreateRootAndLeaf(aaguid);
        var pemPath = Path.GetTempFileName();
        await File.WriteAllTextAsync(pemPath, root.ExportCertificatePem(), Ct);

        try
        {
            var builder = WebApplication.CreateBuilder();
            _ = builder.WebHost.UseTestServer();
            RegisterAttestationTrustPolicy(builder, pemPath, aaguid);

            await using var app = builder.Build();
            await app.Services.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(Ct);

            var policy = app.Services.GetServices<IAttestationTrustPolicy>()
                .OfType<KnownAaguidTrustPolicy>()
                .ShouldHaveSingleItem();

            var context = new AttestationTrustContext
            {
                UserSubjectId = UserSubjectId.New(),
                Aaguid = aaguid,
                AttestationFormat = "packed",
                CertificateChain = [leaf.Export(X509ContentType.Cert)]
            };

            // Evaluating against the AAGUID and root configured above confirms the policy resolved
            // from DI is actually using the registered TrustedAuthenticatorRoots, not some other map.
            var result = await policy.EvaluateAsync(context, Ct);
            _ = result.ShouldBeOfType<AttestationTrustPolicyResult.Accepted>();
        }
        finally
        {
            File.Delete(pemPath);
            leaf.Dispose();
            root.Dispose();
        }
    }

    [Fact]
    public async Task None_attestation_is_not_trusted_even_with_an_allowed_aaguid()
    {
        var aaguid = Guid.NewGuid();
        var (_, trustedRoot) = CreateRootAndLeaf(aaguid);
        var policy = new KnownAaguidTrustPolicy(new TrustedAuthenticatorRoots(new Dictionary<Guid, X509Certificate2Collection>
        {
            [aaguid] = new(trustedRoot)
        }));
        var context = new AttestationTrustContext
        {
            UserSubjectId = UserSubjectId.New(),
            Aaguid = aaguid, // on the allow-list, but "none" attestation provides no proof of this
            AttestationFormat = "none",
            CertificateChain = null
        };

        var result = await policy.EvaluateAsync(context, Ct);

        _ = result.ShouldBeOfType<AttestationTrustPolicyResult.Rejected>();
        trustedRoot.Dispose();
    }

    [Fact]
    public async Task Allowed_aaguid_with_a_chain_to_its_trusted_root_is_accepted()
    {
        var aaguid = Guid.NewGuid();
        var (leaf, root) = CreateRootAndLeaf(aaguid);
        var policy = new KnownAaguidTrustPolicy(new TrustedAuthenticatorRoots(new Dictionary<Guid, X509Certificate2Collection>
        {
            [aaguid] = new(root)
        }));
        var context = new AttestationTrustContext
        {
            UserSubjectId = UserSubjectId.New(),
            Aaguid = aaguid,
            AttestationFormat = "packed",
            CertificateChain = [leaf.Export(X509ContentType.Cert)]
        };

        var result = await policy.EvaluateAsync(context, Ct);

        _ = result.ShouldBeOfType<AttestationTrustPolicyResult.Accepted>();
        leaf.Dispose();
        root.Dispose();
    }

    [Fact]
    public async Task Self_signed_leaf_with_an_allowed_aaguid_is_rejected()
    {
        var aaguid = Guid.NewGuid();
        var (_, trustedRoot) = CreateRootAndLeaf(aaguid);
        using var selfSignedLeaf = CreateSelfSignedCertificate("CN=Untrusted Authenticator");
        var policy = new KnownAaguidTrustPolicy(new TrustedAuthenticatorRoots(new Dictionary<Guid, X509Certificate2Collection>
        {
            [aaguid] = new(trustedRoot)
        }));
        var context = new AttestationTrustContext
        {
            UserSubjectId = UserSubjectId.New(),
            Aaguid = aaguid,
            AttestationFormat = "packed",
            CertificateChain = [selfSignedLeaf.Export(X509ContentType.Cert)]
        };

        var result = await policy.EvaluateAsync(context, Ct);

        _ = result.ShouldBeOfType<AttestationTrustPolicyResult.Rejected>();
        trustedRoot.Dispose();
    }

    [Fact]
    public async Task Allowed_aaguid_with_a_chain_to_a_different_vendors_root_is_rejected()
    {
        var aaguid = Guid.NewGuid();
        var (leaf, _) = CreateRootAndLeaf(aaguid);
        using var otherVendorRoot = CreateSelfSignedCertificate("CN=Other Vendor Root", isCa: true);
        var policy = new KnownAaguidTrustPolicy(new TrustedAuthenticatorRoots(new Dictionary<Guid, X509Certificate2Collection>
        {
            [aaguid] = new(otherVendorRoot)
        }));
        var context = new AttestationTrustContext
        {
            UserSubjectId = UserSubjectId.New(),
            Aaguid = aaguid,
            AttestationFormat = "packed",
            CertificateChain = [leaf.Export(X509ContentType.Cert)]
        };

        var result = await policy.EvaluateAsync(context, Ct);

        _ = result.ShouldBeOfType<AttestationTrustPolicyResult.Rejected>();
        leaf.Dispose();
    }

    private static (X509Certificate2 Leaf, X509Certificate2 Root) CreateRootAndLeaf(Guid aaguid)
    {
        var now = DateTimeOffset.UtcNow;

        using var rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var rootRequest = new CertificateRequest($"CN=Vendor Root {aaguid}", rootKey, HashAlgorithmName.SHA256);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var rootWithKey = rootRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(365));

        using var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var leafRequest = new CertificateRequest($"CN=Authenticator {aaguid}", leafKey, HashAlgorithmName.SHA256);
        using var leafWithoutKey = leafRequest.Create(rootWithKey, now.AddDays(-1), now.AddDays(365), [1, 2, 3]);
        using var leafWithKey = leafWithoutKey.CopyWithPrivateKey(leafKey);

        // Export/reimport so the returned certificates carry only the public key, matching what
        // X509CertificateLoader.LoadCertificate returns for a real attestation certificate delivered
        // over the wire (no private-key handle tied to this method's local keys).
        var root = X509CertificateLoader.LoadCertificate(rootWithKey.Export(X509ContentType.Cert));
        var leaf = X509CertificateLoader.LoadCertificate(leafWithKey.Export(X509ContentType.Cert));

        return (leaf, root);
    }

    private static X509Certificate2 CreateSelfSignedCertificate(string subjectName, bool isCa = false)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subjectName, key, HashAlgorithmName.SHA256);
        if (isCa)
        {
            request.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, false, 0, true));
        }

        var now = DateTimeOffset.UtcNow;
        using var created = request.CreateSelfSigned(now.AddDays(-1), now.AddDays(365));
        return X509CertificateLoader.LoadCertificate(created.Export(X509ContentType.Cert));
    }
}

// begin-snippet: passkeys-signature-verifier
// A skeleton for an algorithm not already built in (EdDSA/Ed25519). .NET has no built-in Ed25519
// verification, so this must call into a third-party crypto library before it can be registered.
// Registering this verifier adds its algorithm to the default pubKeyCredParams offered during
// registration (see PasskeyOptions.SupportedAlgorithms), so it must not be registered until
// VerifySignature actually verifies: until then it fails closed, rejecting every signature.
internal sealed class Ed25519SignatureVerifier : ISignatureVerifier
{
    public int Algorithm => -8; // EdDSA (Ed25519)

    public bool VerifySignature(byte[] publicKeyCbor, byte[] data, byte[]? signature) =>
        throw new NotImplementedException(
            "Verify the Ed25519 signature with a third-party crypto library (.NET has no built-in " +
            "Ed25519 support) before registering this verifier.");
}
// end-snippet

// begin-snippet: passkeys-trust-policy
// A trust policy that only accepts authenticators from a known set of AAGUIDs, each bound to the
// vendor root(s) that are allowed to vouch for it.
//
// The AAGUID in AttestationTrustContext comes from the attested credential data the authenticator
// itself reports, so it proves nothing on its own: any authenticator can claim any AAGUID. The format
// validators (e.g. PackedAttestationFormatValidator) only check the attestation signature against the
// leaf certificate they're given - they never check that certificate against a trusted root. Building
// the chain to a trusted root, and binding that root to the claimed AAGUID, is this policy's job.
internal sealed record TrustedAuthenticatorRoots(IReadOnlyDictionary<Guid, X509Certificate2Collection> RootsByAaguid);

internal sealed class KnownAaguidTrustPolicy : IAttestationTrustPolicy
{
    private readonly IReadOnlyDictionary<Guid, X509Certificate2Collection> _trustedRootsByAaguid;

    public KnownAaguidTrustPolicy(TrustedAuthenticatorRoots trustedRoots) =>
        _trustedRootsByAaguid = trustedRoots.RootsByAaguid;

    public ValueTask<AttestationTrustPolicyResult> EvaluateAsync(AttestationTrustContext context, Ct ct)
    {
        if (context.CertificateChain is not { Count: > 0 } chain)
        {
            return ValueTask.FromResult(AttestationTrustPolicyResult.Reject(
                $"Attestation format '{context.AttestationFormat}' has no certificate chain, so the AAGUID cannot be verified."));
        }

        if (!_trustedRootsByAaguid.TryGetValue(context.Aaguid, out var trustedRoots))
        {
            return ValueTask.FromResult(AttestationTrustPolicyResult.Reject("authenticator is not on the allowed list"));
        }

        return ValueTask.FromResult(ChainBuildsToATrustedRoot(chain, trustedRoots)
            ? AttestationTrustPolicyResult.Accept()
            : AttestationTrustPolicyResult.Reject(
                $"Attestation certificate chain does not build to a trusted root for AAGUID '{context.Aaguid}'."));
    }

    private static bool ChainBuildsToATrustedRoot(IReadOnlyList<byte[]> chain, X509Certificate2Collection trustedRoots)
    {
        using var leaf = X509CertificateLoader.LoadCertificate(chain[0]);
        using var x509Chain = new X509Chain();

        // CustomRootTrust ignores the machine/user trust stores entirely: the chain is trusted only
        // if it builds to one of the certificates below, never to a public CA.
        x509Chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        x509Chain.ChainPolicy.CustomTrustStore.AddRange(trustedRoots);

        // Attestation roots are long-lived vendor/MDS certificates with no CRL/OCSP endpoint a relying
        // party can reach, so there's nothing to check revocation against.
        x509Chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

        try
        {
            for (var i = 1; i < chain.Count; i++)
            {
                _ = x509Chain.ChainPolicy.ExtraStore.Add(X509CertificateLoader.LoadCertificate(chain[i]));
            }

            // With CustomRootTrust, Build only succeeds when the chain terminates in a certificate
            // from CustomTrustStore, so a chain to a different (even otherwise valid) root fails here.
            return x509Chain.Build(leaf);
        }
        finally
        {
            foreach (var intermediate in x509Chain.ChainPolicy.ExtraStore)
            {
                intermediate.Dispose();
            }
        }
    }
}
// end-snippet
