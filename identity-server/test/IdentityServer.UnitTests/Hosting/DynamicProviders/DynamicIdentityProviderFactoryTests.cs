// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Hosting.DynamicProviders;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace UnitTests.Hosting.DynamicProviders;

public class DynamicIdentityProviderFactoryTests
{
    [Fact]
    public void Create_returns_null_for_unregistered_type()
    {
        var options = new DynamicProviderOptions();
        var factory = new DynamicIdentityProviderFactory(options);

        var result = factory.Create(new IdentityProvider("unregistered") { Scheme = "test" });

        result.ShouldBeNull();
    }

    [Fact]
    public void Create_returns_derived_type_with_copy_constructor()
    {
        var options = new DynamicProviderOptions();
        options.AddProviderType<OpenIdConnectHandler, OpenIdConnectOptions, OidcProvider>("oidc");
        var factory = new DynamicIdentityProviderFactory(options);

        var baseModel = new IdentityProvider("oidc")
        {
            Scheme = "my-oidc",
            DisplayName = "My OIDC"
        };

        var result = factory.Create(baseModel);

        result.ShouldNotBeNull();
        result.ShouldBeOfType<OidcProvider>();
        result.Scheme.ShouldBe("my-oidc");
        result.DisplayName.ShouldBe("My OIDC");
    }

    [Fact]
    public void AddProviderType_throws_when_copy_constructor_is_missing()
    {
        var options = new DynamicProviderOptions();

        var ex = Should.Throw<InvalidOperationException>(
            () => options.AddProviderType<OpenIdConnectHandler, OpenIdConnectOptions, ProviderWithoutCopyCtor>("bad"));
        ex.Message.ShouldContain(nameof(ProviderWithoutCopyCtor));
        ex.Message.ShouldContain("copy constructor");
    }

    private record ProviderWithoutCopyCtor : IdentityProvider
    {
        public ProviderWithoutCopyCtor() : base("bad") { }
        // Intentionally missing: ctor(IdentityProvider)
    }
}
