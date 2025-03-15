// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Cryptography;
using Duende.IdentityServer;
using Duende.IdentityServer.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace UnitTests.Extensions;

public class IdentityServerBuilderExtensionsCryptoTests
{
    [Fact]
    public void AddSigningCredential_with_json_web_key_containing_asymmetric_key_should_succeed()
    {
        IServiceCollection services = new ServiceCollection();
        IIdentityServerBuilder identityServerBuilder = new IdentityServerBuilder(services);

        var json =
            """
            {
                "alg": "RS256",
                "kty": "RSA",
                "use": "sig",
                "d": "KGGNkbbgm2hNMqW6fP1fmcWwEBy77WOJIPAXnDJ0KxNTtqDF8K5ULj7EElHO1A8ZnNl1Ey/x//G9lJCOQUU9wmj010dOSsW0NBbR5NtRtLLuVbkVdyft53PGeTQs+1S3c51fz9jojtNqmlfXSANPFOH6QhxmzpTx3KLsf/TpCzblkSrEGOOqCCvVdl7ybTcB230jNhh3JoL7po1rvxKtoOM4a/Bs0NtKj7e+VaHcf0GLnBPJYetsHu43ZfNejJeDoouaXZzeVEklY3B0pe10OTCIOu0JUKGZxNekklRIo1WSEYdL+CJfrSKWIv8bLj6xSr5zrASvWODyH443LN6ZvQ==",
                "e": "AQAB",
                "n": "q7mZfquRq8tzg/5slbNdQmrosNN/mFXS25dbSPm11qEDCgZa452KkO8+hvMtqa92QaqdlmalSF8+FRDOz3grDR5NtmnXZxuKnp+raKfzpC6hCvh2JSIe/J9enmsMM4YeI4d1FOSDwhJlZIYMdMnqG/VJtO1LSHjOaF3XN31ANKF0nPAsmr2/WysiQlxnxxiikLEnsFuNdS615ODDXFGTQ1E+zc4zVur4/Ox0cllPwHPA4PqoIgdPJPL+xM9IOIXuAGtsp4CYoxT6VWaRrALIZXXDY806WGTuctq4KKot6FGL9HQte2hRLl4E/r8SzIK86U3wRwrBe7saK+XUXoP0gQ=",
                "p": "25dkucyCSqxRcJpRrhl7PXqw7wqBZeLQgYlZLpK493PdM8pFfq+/LK1hFtxIjdFKqXS/TOikB4YCBMEH0Im3HZ8Lo0dub3SWNhdegJyRjMbcoO+A9YSODEj7DFaNpZtdmtDi1n6etJm66ctPSR20NNpzoYZuaJ92fVQiKiOh6Qs=",
                "q": "yDKBrS8l1DOx4dwP9hdwhqZJ3XahidiIZSL7m46I/6+cjaki/1mtNiA60MOgqTKegP7Fo7jAYvliqQwnvVGmQvLv19cfKywlIuKN9DdkLHnKh75hfo7aakEbO7GJ5zVgsNnKOdf8wvpclfvIuRDEVva4cksPzsJy6K7C8ENCSCM=",
                "dp": "GlYJ6o6wgawxCEQ5z5uWwETau5CS/Fk7kI2ceI14SZVHzlJQC2WglAcnQcqhmQCk57Xsy5iLM6vKyi8sdMJPh+nvR2HlyNA+w7YBy4L7odqn01VmLgv7zVVjZpNq4ZXEoDC1Q+xjtF1LoYaUt7wsRLp+a7znuPyHBXj1sAAeBwk=",
                "dq": "W8OK3S83T8VCTBzq1Ap6cb3XLcQq11yBaJpYaj0zXr/IKsbUW+dnFeBAFWEWS3gAX3Bod1tAFB3rs0D3FjhO1XE1ruHUT520iAEAwGiDaj+JLh994NzqELo3GW2PoIM/BtFNeKYgHd9UgQsgPnQJCzOb6Aev/z3yHeW9RRQPVbE=",
                "qi": "w4KdmiDN1GtK71JxaasqmEKPNfV3v2KZDXKnfyhUsdx/idKbdTVjvMOkxFPJ4FqV4yIVn06f3QHTm4NEG18Diqxsrzd6kXQIHOa858tLsCcmt9FoGfrgCFgVceh3K/Zah/r8rl9Y61u0Z1kZumwMvFpFE+mVU01t9HgTEAVkHTc="
            }
            """;

        JsonWebKey jsonWebKey = new JsonWebKey(json);
        SigningCredentials credentials = new SigningCredentials(jsonWebKey, jsonWebKey.Alg);
        identityServerBuilder.AddSigningCredential(credentials);
    }

    [Fact]
    public void AddSigningCredential_with_json_web_key_containing_symmetric_key_should_throw_exception()
    {
        IServiceCollection services = new ServiceCollection();
        IIdentityServerBuilder identityServerBuilder = new IdentityServerBuilder(services);

        string json =
            """
            {
                "alg" : "HS256",
                "kty" : "oct",
                "use" : "sig",
                "k" : "y5FHaQFtC294HLAtPXAcMkxZ5gHzCq24223vSYQUrDuu-3CUw7UzPru-AX30ubeB2IM_gUsNQ80bX22wwSk_3LC6XxYxqeGJZSeoQqHG0VNbaWCVkqeuB_HOiL1-ksPfGT-o8_A_Uv-6zi2NaEOYpnIyff5LpdW__LhiE-bhIenaw7GhoXSAfsGEZfNZpUUOU35NAiN2dv0T5vptb87wkL1I2zLhV0pdLvWsDWgQPINEa8bbCA_mseBYpB1eioZvt0TZbp6CL9tiEoiikYV_F3IutrJ2SOWYtDNFeQ3sbyYP7zTzh9a2eyaM8ca5_q3qosI92AbZ7WpEFLa9cZ_O7g"
            }
            """;

        JsonWebKey jsonWebKey = new JsonWebKey(json);
        SigningCredentials credentials = new SigningCredentials(jsonWebKey, jsonWebKey.Alg);
        var act = () => identityServerBuilder.AddSigningCredential(credentials);

        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void AddDeveloperSigningCredential_should_succeed()
    {
        IServiceCollection services = new ServiceCollection();
        IIdentityServerBuilder identityServerBuilder = new IdentityServerBuilder(services);

        identityServerBuilder.AddDeveloperSigningCredential();

        //clean up... delete stored rsa key
        var filename = Path.Combine(Directory.GetCurrentDirectory(), "tempkey.rsa");

        if (File.Exists(filename))
            File.Delete(filename);
    }

    [Fact]
    public void AddDeveloperSigningCredential_should_succeed_when_called_multiple_times()
    {
        IServiceCollection services = new ServiceCollection();
        IIdentityServerBuilder identityServerBuilder = new IdentityServerBuilder(services);

        try
        {
            identityServerBuilder.AddDeveloperSigningCredential();

            //calling a second time will try to load the saved rsa key from disk. An exception will be throw if the private key is not serialized properly.
            identityServerBuilder.AddDeveloperSigningCredential();
        }
        finally
        {
            //clean up... delete stored rsa key
            var filename = Path.Combine(Directory.GetCurrentDirectory(), "tempkey.rsa");

            if (File.Exists(filename))
                File.Delete(filename);
        }
    }

    [Theory]
    [InlineData(Constants.CurveOids.P256, SecurityAlgorithms.EcdsaSha256)]
    [InlineData(Constants.CurveOids.P384, SecurityAlgorithms.EcdsaSha384)]
    [InlineData(Constants.CurveOids.P521, SecurityAlgorithms.EcdsaSha512)]
    public void AddSigningCredential_with_valid_curve_should_succeed(string curveOid, string alg)
    {
        IServiceCollection services = new ServiceCollection();
        IIdentityServerBuilder identityServerBuilder = new IdentityServerBuilder(services);

        var key = new ECDsaSecurityKey(ECDsa.Create(
            ECCurve.CreateFromOid(Oid.FromOidValue(curveOid, OidGroup.All))));

        identityServerBuilder.AddSigningCredential(key, alg);
    }

    [Theory]
    [InlineData(Constants.CurveOids.P256, SecurityAlgorithms.EcdsaSha512)]
    [InlineData(Constants.CurveOids.P384, SecurityAlgorithms.EcdsaSha512)]
    [InlineData(Constants.CurveOids.P521, SecurityAlgorithms.EcdsaSha256)]
    public void AddSigningCredential_with_invalid_curve_should_throw_exception(string curveOid, string alg)
    {
        IServiceCollection services = new ServiceCollection();
        IIdentityServerBuilder identityServerBuilder = new IdentityServerBuilder(services);

        var key = new ECDsaSecurityKey(ECDsa.Create(
            ECCurve.CreateFromOid(Oid.FromOidValue(curveOid, OidGroup.All))));

        var act = () => identityServerBuilder.AddSigningCredential(key, alg);
        act.ShouldThrow<InvalidOperationException>();
    }

    [Theory]
    [InlineData(Constants.CurveOids.P256, SecurityAlgorithms.EcdsaSha256, JsonWebKeyECTypes.P256)]
    [InlineData(Constants.CurveOids.P384, SecurityAlgorithms.EcdsaSha384, JsonWebKeyECTypes.P384)]
    [InlineData(Constants.CurveOids.P521, SecurityAlgorithms.EcdsaSha512, JsonWebKeyECTypes.P521)]
    public void AddSigningCredential_with_invalid_crv_value_should_throw_exception(string curveOid, string alg, string crv)
    {
        IServiceCollection services = new ServiceCollection();
        IIdentityServerBuilder identityServerBuilder = new IdentityServerBuilder(services);

        var key = new ECDsaSecurityKey(ECDsa.Create(
            ECCurve.CreateFromOid(Oid.FromOidValue(curveOid, OidGroup.All))));
        var parameters = key.ECDsa.ExportParameters(true);

        var jsonWebKeyFromECDsa = new JsonWebKey()
        {
            Kty = JsonWebAlgorithmsKeyTypes.EllipticCurve,
            Use = "sig",
            Kid = key.KeyId,
            KeyId = key.KeyId,
            X = Base64UrlEncoder.Encode(parameters.Q.X),
            Y = Base64UrlEncoder.Encode(parameters.Q.Y),
            D = Base64UrlEncoder.Encode(parameters.D),
            Crv = crv.Replace("-", string.Empty),
            Alg = SecurityAlgorithms.EcdsaSha256
        };
        var act = () => identityServerBuilder.AddSigningCredential(jsonWebKeyFromECDsa, alg);

        act.ShouldThrow<InvalidOperationException>();
    }
}
