// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Duende.Platform.UserManagement.Scim.Groups;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Scim;
using Duende.UserManagement.TestIsolation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimFixture : IAsyncDisposable
{
    private const string TestIssuer = "https://test-issuer";
    private const string TestAudience = "urn:duende:scim";

    private static readonly byte[] SigningKeyBytes = Encoding.UTF8.GetBytes("ThisIsATestSigningKeyWith32Bytes");
    private static readonly SymmetricSecurityKey SigningKey = new(SigningKeyBytes);

    private readonly ITestOutputHelper _output;
    private readonly WebServerFixture _serverFixture;
    private KestrelBasedTestServer? _server;
    public ScimHttpClient Client { get; private set; } = null!;
    public ScimGroupHttpClient GroupClient { get; private set; } = null!;

    public IUserProfileAdmin UserProfileAdmin { get; private set; } = null!;
    public IUserProfileSchemaAdmin UserSchemaAdmin { get; private set; } = null!;
    public IGroupAdmin GroupAdmin { get; private set; } = null!;
    public IMembershipAdmin MembershipAdmin { get; private set; } = null!;
    public IUserAuthenticatorsAdmin AuthenticatorsAdmin { get; private set; } = null!;

    public Action<IServiceCollection> ConfigureServices { get; set; } = s => { };
    public Action<IUserManagementBuilder> ConfigurePlatform { get; set; } = s => { };
    public Action<ScimEndpointOptions> ConfigureScimOptions { get; set; } = s => { };
    public Action<ScimOptions> ConfigureScimCapabilities { get; set; } = s => { };

#pragma warning disable duende_experimental
    public Action<ScimOAuthOptions> ConfigureScimAuthOptions { get; set; } = s => { };
#pragma warning restore duende_experimental

    public ScimFixture(
        ITestOutputHelper output,
        WebServerFixture serverFixture)
    {
        _output = output;
        _serverFixture = serverFixture;
    }

    public async ValueTask InitializeAsync()
    {
        var dbId = Guid.NewGuid();

        _server = new KestrelBasedTestServer(
            "scim",
            _serverFixture,
            _output,
            configureServices: services =>
            {
                ConfigureServices(services);
                _ = services.AddAuthentication();
                var builder = services.AddUserManagementInternal(users =>
                {
                    // modules registered unconditionally by AddUserManagementInternal

#pragma warning disable duende_experimental
                    _ = users.EnableScim(x => ConfigureScimCapabilities(x), x => ConfigureScimOptions(x));
                    _ = users.ConfigureScimOAuth(x =>
                    {
                        x.Authority = TestIssuer;
                        x.RequireHttpsMetadata = false;
                        ConfigureScimAuthOptions(x);
                    });
#pragma warning restore duende_experimental

                    ConfigurePlatform(users);
                    _ = users.AddSqliteStore(opt => opt.ConnectionString = $"Data Source=MySharedDb_{dbId};Mode=Memory;Cache=Shared");
                });

                // Post-configure the JWT bearer handler to use a symmetric key (no OIDC discovery)
                _ = services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, TestJwtBearerPostConfigureOptions>();
            },
            configurePipeline: app =>
            {
                _ = app.Use(async (c, n) =>
                {
                    try
                    {
                        await n();
                    }
                    catch (Exception ex)
                    {
                        _output.WriteLine("Unhandled exception: {0}", ex);
                        throw;
                    }
                });

                _ = app.UseAuthorization();

#pragma warning disable duende_experimental
                _ = app.MapScim();
#pragma warning restore duende_experimental
            }
        );

        await _server.StartAsync();

        await _server.GetRequiredService<IPooledStore>().MigrateAsync(TestContext.Current.CancellationToken);

        UserProfileAdmin = _server.Services.GetRequiredService<IUserProfileAdmin>();
        UserSchemaAdmin = _server.Services.GetRequiredService<IUserProfileSchemaAdmin>();
        GroupAdmin = _server.Services.GetRequiredService<IGroupAdmin>();
        MembershipAdmin = _server.Services.GetRequiredService<IMembershipAdmin>();
        AuthenticatorsAdmin = _server.Services.GetRequiredService<IUserAuthenticatorsAdmin>();

        Client = BuildScimClient(null);
        Client.SetBearerToken(CreateAccessToken("scim"));
        GroupClient = BuildScimGroupClient(null);
        GroupClient.SetBearerToken(CreateAccessToken("scim"));

        // userName is always stored as a profile attribute; register with uniqueness
        await RegisterAttributeDefinitionAsync("username", ScalarDataType.String, "User login name", isUnique: true);
    }

    public ScimHttpClient BuildScimClient(string? customRoute)
    {
#pragma warning disable CA2000 // HttpMessageHandler is owned by the ScimHttpClient and will be disposed when the client is disposed
        var handler = TestIsolationService.CreateHandler(allowAutoRedirect: false);
#pragma warning restore CA2000
        var baseAddress = _server!.BaseAddress;
        return customRoute == null
            ? new ScimHttpClient(handler, baseAddress)
            : new ScimHttpClient(handler, baseAddress, customRoute);
    }

    public ScimGroupHttpClient BuildScimGroupClient(string? customRoute)
    {
#pragma warning disable CA2000 // HttpMessageHandler is owned by the ScimGroupHttpClient and will be disposed when the client is disposed
        var handler = TestIsolationService.CreateHandler(allowAutoRedirect: false);
#pragma warning restore CA2000
        var baseAddress = _server!.BaseAddress;
        return customRoute == null
            ? new ScimGroupHttpClient(handler, baseAddress)
            : new ScimGroupHttpClient(handler, baseAddress, customRoute);
    }

    /// <summary>
    /// Creates a user profile via the SCIM Users endpoint and returns the user's subject ID.
    /// </summary>
    public async Task<string> CreateUserAsync(string userName)
    {
        var (response, body) = await Client.CreateUserAsync(userName);
        _ = response.EnsureSuccessStatusCode();
        return ScimHttpClient.GetUserId(body);
    }

    /// <summary>
    /// Mints a signed JWT access token for testing SCIM endpoint authentication.
    /// </summary>
    public static string CreateAccessToken(params string[] scopes) =>
        CreateAccessToken(TestIssuer, TestAudience, SigningKey, TimeSpan.FromHours(1), scopes);

    /// <summary>
    /// Mints a signed JWT access token with explicit parameters for negative-path tests.
    /// </summary>
    public static string CreateAccessToken(
        string issuer,
        string audience,
        SecurityKey signingKey,
        TimeSpan lifetime,
        params string[] scopes)
    {
        var claims = new List<Claim>();
        foreach (var scope in scopes)
        {
            claims.Add(new Claim("scope", scope));
        }

        var now = DateTime.UtcNow;
        var expires = now + lifetime;
        var notBefore = lifetime < TimeSpan.Zero ? expires.AddHours(-1) : now;
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: notBefore,
            expires: expires,
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Creates a token signed with a different key (for invalid-signature tests).
    /// </summary>
    public static string CreateTokenWithWrongSignature(params string[] scopes)
    {
        var wrongKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("ADifferentKeyThatIsAlso32Bytes!!"));
        return CreateAccessToken(TestIssuer, TestAudience, wrongKey, TimeSpan.FromHours(1), scopes);
    }

    /// <summary>
    /// Creates an expired token (for expired-token tests).
    /// </summary>
    public static string CreateExpiredToken(params string[] scopes) =>
        CreateAccessToken(TestIssuer, TestAudience, SigningKey, TimeSpan.FromHours(-1), scopes);

    /// <summary>
    /// Creates a token with the wrong audience (for audience-validation tests).
    /// </summary>
    public static string CreateTokenWithWrongAudience(params string[] scopes) =>
        CreateAccessToken(TestIssuer, "wrong-audience", SigningKey, TimeSpan.FromHours(1), scopes);

    /// <summary>
    /// Creates a token with arbitrary claims (for custom-policy tests).
    /// </summary>
    public static string CreateTokenWithCustomClaims(params Claim[] claims)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            notBefore: now,
            expires: now + TimeSpan.FromHours(1),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Creates a token with a space-delimited scope claim (single claim, multiple scopes).
    /// </summary>
    public static string CreateTokenWithSpaceDelimitedScopes(params string[] scopes)
    {
        var scopeValue = string.Join(' ', scopes);
        var claims = new List<Claim> { new("scope", scopeValue) };

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            notBefore: now,
            expires: now + TimeSpan.FromHours(1),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Registers a custom schema attribute via the admin API.
    /// Must be called after <see cref="InitializeAsync"/>.
    /// </summary>
    public async Task RegisterAttributeDefinitionAsync(
        string name,
        ScalarDataType dataType,
        string description,
        bool isUnique = false)
    {
        var ct = TestContext.Current.CancellationToken;
        _ = await UserSchemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create(name),
                AttributeType = new ScalarAttributeType(dataType),
                Description = AttributeDescription.Create(description),
                IsUnique = isUnique
            },
            ct);
    }

    /// <summary>
    /// Registers a custom schema attribute via the admin API using any <see cref="AttributeType"/>.
    /// Must be called after <see cref="InitializeAsync"/>.
    /// </summary>
    public async Task RegisterComplexAttributeDefinitionAsync(
        string name,
        AttributeType attributeType,
        string description)
    {
        var ct = TestContext.Current.CancellationToken;
        _ = await UserSchemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition
            {
                Code = AttributeCode.Create(name),
                AttributeType = attributeType,
                Description = AttributeDescription.Create(description)
            },
            ct);
    }

    /// <summary>
    /// Registers the RFC 7643 §4.1 User schema attributes:
    /// <list type="bullet">
    ///   <item><c>name</c> — complex with givenname, familyname, formatted, middlename, honorificprefix, honorificsuffix</item>
    ///   <item><c>emails</c> — list of complex with value, type, primary, display</item>
    ///   <item><c>phonenumbers</c> — list of complex with value, type, primary, display</item>
    ///   <item><c>addresses</c> — list of complex with streetaddress, locality, region, postalcode, country, formatted, type, primary</item>
    ///   <item>Scalar: displayname, nickname, title, active, profileurl, usertype, preferredlanguage, locale, timezone, externalid</item>
    /// </list>
    /// Must be called after <see cref="InitializeAsync"/>.
    /// </summary>
    public async Task RegisterScimUserSchemaAsync()
    {
        // name — complex
        var nameType = new ComplexAttributeType(
            new Dictionary<AttributeCode, ComplexAttributeProperty>
            {
                [AttributeCode.Create("givenName")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("familyName")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("formatted")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("middleName")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("honorificPrefix")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("honorificSuffix")] = ComplexAttributeProperty.Of(ScalarDataType.String)
            });
        await RegisterComplexAttributeDefinitionAsync("name", nameType, "Full name of the User");

        // emails — list of complex
        var emailElementType = new ComplexAttributeType(
            new Dictionary<AttributeCode, ComplexAttributeProperty>
            {
                [AttributeCode.Create("value")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("primary")] = ComplexAttributeProperty.Of(ScalarDataType.Boolean),
                [AttributeCode.Create("display")] = ComplexAttributeProperty.Of(ScalarDataType.String)
            });
        await RegisterComplexAttributeDefinitionAsync("emails", new ListAttributeType(emailElementType), "Email addresses");

        // phonenumbers — list of complex
        var phoneElementType = new ComplexAttributeType(
            new Dictionary<AttributeCode, ComplexAttributeProperty>
            {
                [AttributeCode.Create("value")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("primary")] = ComplexAttributeProperty.Of(ScalarDataType.Boolean),
                [AttributeCode.Create("display")] = ComplexAttributeProperty.Of(ScalarDataType.String)
            });
        await RegisterComplexAttributeDefinitionAsync("phoneNumbers", new ListAttributeType(phoneElementType), "Phone numbers");

        // addresses — list of complex
        var addressElementType = new ComplexAttributeType(
            new Dictionary<AttributeCode, ComplexAttributeProperty>
            {
                [AttributeCode.Create("streetAddress")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("locality")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("region")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("postalCode")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("country")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("formatted")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
                [AttributeCode.Create("primary")] = ComplexAttributeProperty.Of(ScalarDataType.Boolean)
            });
        await RegisterComplexAttributeDefinitionAsync("addresses", new ListAttributeType(addressElementType), "Physical addresses");

        // Scalar attributes
        await RegisterAttributeDefinitionAsync("displayName", ScalarDataType.String, "Display name");
        await RegisterAttributeDefinitionAsync("nickName", ScalarDataType.String, "Casual name");
        await RegisterAttributeDefinitionAsync("title", ScalarDataType.String, "Job title");
        await RegisterAttributeDefinitionAsync("active", ScalarDataType.Boolean, "Account status");
        await RegisterAttributeDefinitionAsync("profileUrl", ScalarDataType.String, "Profile URL");
        await RegisterAttributeDefinitionAsync("userType", ScalarDataType.String, "User type");
        await RegisterAttributeDefinitionAsync("preferredLanguage", ScalarDataType.String, "Preferred language");
        await RegisterAttributeDefinitionAsync("locale", ScalarDataType.String, "Locale");
        await RegisterAttributeDefinitionAsync("timezone", ScalarDataType.String, "Time zone");
        await RegisterAttributeDefinitionAsync("externalId", ScalarDataType.String, "External ID");
    }

    public async ValueTask DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.DisposeAsync();
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Post-configures <see cref="JwtBearerOptions"/> for the SCIM authentication scheme
    /// to use a symmetric signing key and disable OIDC discovery in tests.
    /// </summary>
    private sealed class TestJwtBearerPostConfigureOptions : IPostConfigureOptions<JwtBearerOptions>
    {
        public void PostConfigure(string? name, JwtBearerOptions options)
        {
            if (name != Duende.UserManagement.Scim.Internal.ScimConstants.AuthenticationScheme)
            {
                return;
            }

            // Disable metadata discovery — use manual configuration
            options.Configuration = new OpenIdConnectConfiguration();
            options.TokenValidationParameters.IssuerSigningKey = SigningKey;
            options.TokenValidationParameters.ValidIssuer = TestIssuer;
            options.TokenValidationParameters.ValidateAudience = true;
            options.TokenValidationParameters.ValidAudiences = [TestAudience];
            options.TokenValidationParameters.ValidateLifetime = true;
            options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        }
    }
}
