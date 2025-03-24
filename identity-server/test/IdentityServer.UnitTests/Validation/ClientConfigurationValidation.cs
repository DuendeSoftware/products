// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Validation;
using UnitTests.Validation.Setup;

namespace UnitTests.Validation;

public class ClientConfigurationValidation
{
    private const string Category = "Client Configuration Validation Tests";
    private IClientConfigurationValidator _validator;
    private IdentityServerOptions _options;

    public ClientConfigurationValidation()
    {
        _options = new IdentityServerOptions();
        _validator = new DefaultClientConfigurationValidator(_options);
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Standard_clients_should_succeed()
    {
        foreach (var client in TestClients.Get())
        {
            // deliberate invalid configuration
            if (client.ClientId == "implicit_and_client_creds") continue;

            var context = await ValidateAsync(client);

            if (!context.IsValid)
            {
                throw new System.Exception($"client {client.ClientId} failed configuration validation: {context.ErrorMessage}");
            }

        }
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_access_token_lifetime_should_fail()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            RequireClientSecret = false,
            AllowedScopes = { "foo" },

            AccessTokenLifetime = 0
        };

        await ShouldFailAsync(client, "access token lifetime is 0 or negative");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_identity_token_lifetime_should_fail()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
            RequireClientSecret = false,
            AllowedScopes = { "foo" },

            IdentityTokenLifetime = 0
        };

        await ShouldFailAsync(client, "identity token lifetime is 0 or negative");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_absolute_refresh_token_lifetime_should_fail()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
            RequireClientSecret = false,
            AllowedScopes = { "foo" },

            AbsoluteRefreshTokenLifetime = -1
        };

        await ShouldFailAsync(client, "absolute refresh token lifetime is negative");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Invalid_sliding_refresh_token_lifetime_should_fail()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
            RequireClientSecret = false,
            AllowedScopes = { "foo" },

            SlidingRefreshTokenLifetime = -1
        };

        await ShouldFailAsync(client, "sliding refresh token lifetime is negative");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_allowed_grant_type_should_fail()
    {
        var client = new Client
        {
            ClientId = "id",
            RequireClientSecret = false,
            AllowedScopes = { "foo" },
        };

        await ShouldFailAsync(client, "no allowed grant type specified");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_client_secret_for_client_credentials_should_fail()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            AllowedScopes = { "foo" },
        };

        await ShouldFailAsync(client, "Client secret is required for client_credentials, but no client secret is configured.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_client_secret_for_implicit_and_client_credentials_should_fail()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.ImplicitAndClientCredentials,
            RedirectUris = { "https://foo" },
            AllowedScopes = { "foo" },
        };

        await ShouldFailAsync(client, "Client secret is required for client_credentials, but no client secret is configured.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_client_secret_for_hybrid_should_fail()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.Hybrid,
            RedirectUris = { "https://foo" },
            AllowedScopes = { "foo" },
        };

        await ShouldFailAsync(client, "Client secret is required for hybrid, but no client secret is configured.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_client_secret_for_code_should_fail()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.Code,
            RedirectUris = { "https://foo" },
            AllowedScopes = { "foo" },
        };

        await ShouldFailAsync(client, "Client secret is required for authorization_code, but no client secret is configured.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Not_required_client_secret_for_hybrid_should_succeed()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.Hybrid,
            RequireClientSecret = false,
            RedirectUris = { "https://foo" },
            AllowedScopes = { "foo" },
        };

        var context = await ValidateAsync(client);
        context.IsValid.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(GrantTypesWithClientCredentialsTestData))]
    [Trait("Category", Category)]
    public async Task not_required_client_secret_for_client_credentials_should_fail(ICollection<string> allowedGrantTypes)
    {
        var client = new Client
        {
            ClientId = "id",
            ClientSecrets = { new Secret("secret") },
            AllowedGrantTypes = allowedGrantTypes,
            RequireClientSecret = false,
            RedirectUris = { "https://foo" },
            AllowedScopes = { "foo" }
        };

        await ShouldFailAsync(client, "RequireClientSecret is false, but client is using client credentials grant type.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task Missing_client_secret_for_implicit_should_succeed()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.Implicit,
            AllowedScopes = { "foo" },
            RedirectUris = { "https://foo" }
        };

        var context = await ValidateAsync(client);
        context.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task empty_grant_types_collection_should_fail()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = { },
            AllowedScopes = { "foo" },
            RedirectUris = { "https://foo" }
        };

        var context = await ValidateAsync(client);
        await ShouldFailAsync(client, "no allowed grant type specified");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task null_redirect_uris_collection_should_succeed()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("hash") },
            AllowedScopes = { "foo" },
            RedirectUris = null,
        };

        var context = await ValidateAsync(client);
        context.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task null_post_logout_redirect_uris_collection_should_succeed()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("hash") },
            AllowedScopes = { "foo" },
            PostLogoutRedirectUris = null
        };

        var context = await ValidateAsync(client);
        context.IsValid.ShouldBeTrue();
    }
    [Fact]
    [Trait("Category", Category)]
    public async Task null_redirect_uris_should_succeed()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("hash") },
            AllowedScopes = { "foo" },
            RedirectUris = { null }
        };

        var context = await ValidateAsync(client);
        context.IsValid.ShouldBeTrue();
    }
    [Fact]
    [Trait("Category", Category)]
    public async Task null_post_logout_redirect_uris_should_succeed()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("hash") },
            AllowedScopes = { "foo" },
            PostLogoutRedirectUris = { null }
        };

        var context = await ValidateAsync(client);
        context.IsValid.ShouldBeTrue();
    }
    [Fact]
    [Trait("Category", Category)]
    public async Task empty_redirect_uris_collection_should_succeed()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("hash") },
            AllowedScopes = { "foo" },
            RedirectUris = { },
        };

        var context = await ValidateAsync(client);
        context.IsValid.ShouldBeTrue();
    }
    [Fact]
    [Trait("Category", Category)]
    public async Task empty_post_logout_redirect_uris_collection_should_succeed()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.ClientCredentials,
            ClientSecrets = { new Secret("hash") },
            AllowedScopes = { "foo" },
            PostLogoutRedirectUris = { },
        };

        var context = await ValidateAsync(client);
        context.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ValidateUriSchemesAsync_for_invalid_redirecturi_scheme_should_fail()
    {
        _options.Validation.InvalidRedirectUriPrefixes.Add("custom");
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.Implicit,
            AllowedScopes = { "foo" },
            RedirectUris = { "http://callback", "custom://callback" }
        };

        var result = await ValidateAsync(client);
        await ShouldFailAsync(client, "RedirectUri 'custom://callback' uses invalid scheme. If this scheme should be allowed, then configure it via ValidationOptions.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ValidateUriSchemesAsync_for_null_redirecturi_scheme_should_succeed()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.Implicit,
            AllowedScopes = { "foo" },
            RedirectUris = null
        };

        var result = await ValidateAsync(client);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ValidateUriSchemesAsync_for_valid_redirect_uri_scheme_should_succeed()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.Implicit,
            AllowedScopes = { "foo" },
            RedirectUris = { "http://callback", "custom://callback" }
        };

        var result = await ValidateAsync(client);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ValidateUriSchemesAsync_for_invalid_post_logout_redirect_uri_scheme_should_fail()
    {
        _options.Validation.InvalidRedirectUriPrefixes.Add("custom");
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.Implicit,
            AllowedScopes = { "foo" },
            RedirectUris = { "http://callback" },
            PostLogoutRedirectUris = { "http://postcallback", "custom://postcallback" }
        };

        var result = await ValidateAsync(client);
        await ShouldFailAsync(client, "PostLogoutRedirectUri 'custom://postcallback' uses invalid scheme. If this scheme should be allowed, then configure it via ValidationOptions.");
    }

    [Fact]
    [Trait("Category", Category)]
    public async Task ValidateUriSchemesAsync_for_valid_post_logout_redirect_uri_scheme_should_succeed()
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.Implicit,
            AllowedScopes = { "foo" },
            RedirectUris = { "http://callback" },
            PostLogoutRedirectUris = { "http://postcallback", "custom://postcallback" }
        };

        var result = await ValidateAsync(client);
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData("bad")]
    [InlineData("urn:foo")]
    [InlineData("urn:foo:123")]
    [InlineData("http://foo/")]
    [InlineData("http://foo:80/path")]
    [InlineData("http://foo/path")]
    [InlineData("http://foo:123/path")]
    [InlineData("https://foo:443/path")]
    [InlineData("custom://foo/")]
    [InlineData("custom://foo/path")]
    [InlineData("custom://foo:443/")]
    [InlineData("custom://foo:443/path")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData((string)null)]
    public async Task ValidateAllowedCorsOriginsAsync_should_report_invalid_URL_format(string origin)
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.Implicit,
            RedirectUris = { "http://client" },
            AllowedCorsOrigins = { origin }
        };

        var result = await ValidateAsync(client);
        result.IsValid.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("invalid origin");
        if (!string.IsNullOrWhiteSpace(origin))
        {
            result.ErrorMessage.ShouldContain(origin);
        }
        else
        {
            result.ErrorMessage.ShouldContain("empty value");
        }
    }

    [Theory]
    [Trait("Category", Category)]
    [InlineData("http://foo")]
    [InlineData("http://foo:80")]
    [InlineData("https://foo")]
    [InlineData("http://foo:123")]
    [InlineData("https://foo:456")]
    [InlineData("https://foo:443")]
    [InlineData("custom://foo")]
    [InlineData("custom://foo:443")]
    public async Task ValidateAllowedCorsOriginsAsync_should_allow_valid_formats(string origin)
    {
        var client = new Client
        {
            ClientId = "id",
            AllowedGrantTypes = GrantTypes.Implicit,
            RedirectUris = { "http://client" },
            AllowedCorsOrigins = { origin }
        };

        var result = await ValidateAsync(client);
        result.IsValid.ShouldBeTrue();
    }


    private async Task<ClientConfigurationValidationContext> ValidateAsync(Client client)
    {
        var context = new ClientConfigurationValidationContext(client);
        await _validator.ValidateAsync(context);

        return context;
    }

    private async Task ShouldFailAsync(Client client, string expectedError)
    {
        var context = await ValidateAsync(client);

        context.IsValid.ShouldBeFalse();
        context.ErrorMessage.ShouldBe(expectedError);
    }

    public static TheoryData<ICollection<string>> GrantTypesWithClientCredentialsTestData =>
    [
        GrantTypes.ImplicitAndClientCredentials,
        GrantTypes.CodeAndClientCredentials,
        GrantTypes.HybridAndClientCredentials,
        GrantTypes.ClientCredentials,
        GrantTypes.ResourceOwnerPasswordAndClientCredentials
    ];

}
