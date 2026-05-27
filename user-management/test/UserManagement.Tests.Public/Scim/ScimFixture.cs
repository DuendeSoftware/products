// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Scim.Groups;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.Internal;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Scim;
using Duende.UserManagement.TestIsolation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimFixture : IAsyncDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly WebServerFixture _serverFixture;
    private KestrelBasedTestServer? _server;
    public ScimHttpClient Client { get; private set; } = null!;
    public ScimGroupHttpClient GroupClient { get; private set; } = null!;

    public IUserProfileAdmin UserProfileAdmin { get; private set; } = null!;
    public IUserProfileSchemaAdmin UserSchemaAdmin { get; private set; } = null!;
    public IGroupAdmin GroupAdmin { get; private set; } = null!;
    public IMembershipAdmin MembershipAdmin { get; private set; } = null!;

    public Action<IServiceCollection> ConfigureServices { get; set; } = s => { };
    public Action<IUserManagementBuilder> ConfigurePlatform { get; set; } = s => { };
    public Action<ScimEndpointOptions> ConfigureScimOptions { get; set; } = s => { };
    public Action<ScimOptions> ConfigureScimCapabilities { get; set; } = s => { };

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
                    _ = users.Scim(x => ConfigureScimCapabilities(x), x => ConfigureScimOptions(x));
#pragma warning restore duende_experimental

                    ConfigurePlatform(users);
                    _ = users.AddSqliteStore(opt => opt.ConnectionString = $"Data Source=MySharedDb_{dbId};Mode=Memory;Cache=Shared");
                });
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

        Client = BuildScimClient(null);
        GroupClient = BuildScimGroupClient(null);

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
}
