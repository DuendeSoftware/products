// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

/// <summary>Builds the in-process hosts every part of the harness runs on.</summary>
internal static class HarnessHosting
{
    public static WebApplicationBuilder CreateBuilder(TestClock clock)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<TimeProvider>(clock.TimeProvider);

        // Caches expire entries by their own clock option, not TimeProvider: put them on the test clock too.
        var systemClock = new TestSystemClock(clock);
        builder.Services.Configure<MemoryCacheOptions>(options => options.Clock = systemClock);
        builder.Services.Configure<MemoryDistributedCacheOptions>(options => options.Clock = systemClock);
        return builder;
    }

    /// <summary>
    /// Binds Kestrel to a dynamic loopback port. Public endpoints (load balancers and client apps) use HTTPS with the
    /// harness certificate, so cookies behave as in production (Secure, SameSite=None). Nodes behind a load balancer
    /// use plain HTTP, like a TLS-terminating proxy in front of them.
    /// </summary>
    public static void ListenOnLoopback(this WebApplicationBuilder builder, bool https) =>
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(IPAddress.Loopback, 0, listen =>
        {
            if (https)
            {
                listen.UseHttps(TestCertificate.Instance);
            }
        }));

    public static int GetPort(this WebApplication app)
    {
        var addresses = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Kestrel did not report its address.");
        return new Uri(addresses.Addresses.Single()).Port;
    }
}

/// <summary>
/// The self-signed certificate every public endpoint in the harness serves. Clients created by the harness accept
/// exactly this certificate (see <see cref="VirtualHostRegistry" />), whatever host name they asked for.
/// </summary>
internal static class TestCertificate
{
    private static readonly Lazy<X509Certificate2> Certificate = new(Create);

    public static X509Certificate2 Instance => Certificate.Value;

    public static bool IsHarnessCertificate(X509Certificate? certificate) =>
        certificate is not null && certificate.GetCertHashString() == Instance.GetCertHashString();

    private static X509Certificate2 Create()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=outside-in-test-harness", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var now = DateTimeOffset.UtcNow;
        using var ephemeral = request.CreateSelfSigned(now.AddDays(-1), now.AddYears(1));

        // On Windows, SslStream can't serve a certificate with an ephemeral key, so round-trip through PFX.
        var certificate = X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pfx), password: null);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => certificate.Dispose();
        return certificate;
    }
}

/// <summary>
/// Data protection keys shared by every node of one host (design doc section 03), so cookies and protected state
/// written by one node can be read by the other. Keys live in memory; nothing is written to disk.
/// </summary>
internal sealed class SharedDataProtection
{
    private readonly InMemoryXmlRepository _repository = new();
    private readonly string _applicationName;

    public SharedDataProtection(string applicationName, TestClock clock)
    {
        _applicationName = applicationName;

        // Create the key up front. Otherwise two nodes starting together each create their own default key.
        var services = new ServiceCollection();
        Configure(services);
        using var provider = services.BuildServiceProvider();
        var now = clock.TimeProvider.GetUtcNow();
        provider.GetRequiredService<IKeyManager>().CreateNewKey(now.AddDays(-1), now.AddYears(1));
    }

    public void Configure(IServiceCollection services)
    {
        services.AddDataProtection().SetApplicationName(_applicationName);
        services.Configure<KeyManagementOptions>(options => options.XmlRepository = _repository);
    }

    private sealed class InMemoryXmlRepository : IXmlRepository
    {
        private readonly Lock _lock = new();
        private readonly List<XElement> _elements = [];

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            lock (_lock)
            {
                return _elements.Select(element => new XElement(element)).ToList();
            }
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            lock (_lock)
            {
                _elements.Add(new XElement(element));
            }
        }
    }
}

/// <summary>Settings every OpenID Connect client in the harness gets: the client apps and IdentityServer's own external providers.</summary>
internal static class HarnessOidcClient
{
    /// <summary>Applies the harness settings. <paramref name="clientName" /> names the client in the connection log, for example <c>web</c>.</summary>
    public static void Apply(OpenIdConnectOptions options, string clientName, TestClock clock, VirtualHostRegistry virtualHosts)
    {
        options.TimeProvider = clock.TimeProvider;
        options.BackchannelHttpHandler = virtualHosts.CreateHandler($"{clientName} back channel");
        options.DisableTelemetry = true;

        // Microsoft.IdentityModel checks token lifetimes against the wall clock, not TimeProvider. Check them against
        // the test clock instead, so advancing it expires tokens on the client side too.
        options.TokenValidationParameters.LifetimeValidator = (notBefore, expires, _, parameters) =>
        {
            var now = clock.TimeProvider.GetUtcNow().UtcDateTime;
            return (notBefore is null || now + parameters.ClockSkew >= notBefore)
                && (expires is null || now - parameters.ClockSkew < expires);
        };
    }
}
