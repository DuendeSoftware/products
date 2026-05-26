// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimCreateUserEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task Create_user_returns_201_with_location_and_etag()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.CreateUserAsync("alice");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var id = ScimHttpClient.GetUserId(body);
        id.ShouldNotBeNullOrEmpty();

        _ = response.Headers.Location.ShouldNotBeNull();
        ShouldlyExtensions.ShouldContain(response.Headers.Location!.ToString(), $"/scim/Users/{id}");

        var etag = ScimHttpClient.GetETag(response);
        etag.ShouldNotBeNullOrEmpty();
        etag.ShouldStartWith("W/\"");

        var schemas = body.RootElement.GetProperty("schemas").EnumerateArray()
            .Select(e => e.GetString()).ToList();
        schemas.ShouldContain(ScimHttpClient.UserSchemaUrn);

        body.RootElement.GetProperty("userName").GetString().ShouldBe("alice");

        var meta = body.RootElement.GetProperty("meta");
        meta.GetProperty("resourceType").GetString().ShouldBe("User");
        meta.GetProperty("location").GetString().ShouldNotBeNullOrEmpty();
        meta.GetProperty("version").GetString().ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Create_user_with_externalId_returns_externalId_in_response()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("externalid", ScalarDataType.String, "SCIM externalId");

        var (response, body) = await Fixture.Client.CreateUserAsync("bob", externalId: "ext-123");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        body.RootElement.GetProperty("externalId").GetString().ShouldBe("ext-123");
    }

    [Fact]
    public async Task Create_user_with_custom_attributes()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("department", ScalarDataType.String, "Department");

        var (response, body) = await Fixture.Client.CreateUserAsync(
            "charlie",
            new { department = "Engineering" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        body.RootElement.GetProperty("department").GetString().ShouldBe("Engineering");
    }

    [Fact]
    public async Task Create_user_without_userName_returns_400_invalidValue()
    {
        await Fixture.InitializeAsync();

        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn } };
        var response = await Fixture.Client.PostAsync(ScimHttpClient.UsersRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidValue");
    }

    [Fact]
    public async Task Create_user_with_empty_userName_returns_400()
    {
        await Fixture.InitializeAsync();

        var payload = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "" };
        var response = await Fixture.Client.PostAsync(ScimHttpClient.UsersRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_user_with_wrong_schema_returns_400_invalidSyntax()
    {
        await Fixture.InitializeAsync();

        var payload = new { schemas = new[] { "urn:wrong:schema" }, userName = "x" };
        var response = await Fixture.Client.PostAsync(ScimHttpClient.UsersRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidSyntax");
    }

    [Fact]
    public async Task Create_user_with_null_body_returns_400()
    {
        await Fixture.InitializeAsync();

        var content = new StringContent("", Encoding.UTF8, ScimHttpClient.ScimContentType);
        var response = await Fixture.Client.PostAsync(ScimHttpClient.UsersRoute, content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidSyntax");
    }

    [Fact]
    public async Task Create_user_with_duplicate_userName_returns_409_uniqueness()
    {
        await Fixture.InitializeAsync();

        var (first, _) = await Fixture.Client.CreateUserAsync("alice");
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        var (second, body) = await Fixture.Client.CreateUserAsync("alice");
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("uniqueness");
    }

    [Fact]
    public async Task Create_user_with_unknown_attribute_returns_400()
    {
        await Fixture.InitializeAsync();

        var payload = new Dictionary<string, object>
        {
            ["schemas"] = new[] { ScimHttpClient.UserSchemaUrn },
            ["userName"] = "dave",
            ["unknownAttr"] = "someValue"
        };
        var response = await Fixture.Client.PostAsync(ScimHttpClient.UsersRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidPath");
    }

    [Fact]
    public async Task Create_user_response_content_type_is_scim_json()
    {
        await Fixture.InitializeAsync();

        var (response, _) = await Fixture.Client.CreateUserAsync("eve");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ScimHttpClient.ScimContentType);
    }

    [Fact]
    public async Task Create_user_with_unsupported_content_type_returns_415()
    {
        await Fixture.InitializeAsync();

        var content = new StringContent("{\"userName\":\"frank\"}", Encoding.UTF8, "text/plain");
        var response = await Fixture.Client.PostAsync(ScimHttpClient.UsersRoute, content);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Create_user_with_name_complex_attribute()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (response, body) = await Fixture.Client.CreateUserAsync(
            "alice",
            new
            {
                name = new { givenName = "Alice", familyName = "Smith" }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var user = body.RootElement.Deserialize<ScimUserWithName>();
        _ = user.ShouldNotBeNull();
        user.Name.ShouldBeEquivalentTo(new ScimName { GivenName = "Alice", FamilyName = "Smith" });
    }

    [Fact]
    public async Task Create_user_with_emails_list_attribute()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (response, body) = await Fixture.Client.CreateUserAsync(
            "bob",
            new
            {
                emails = new[]
                {
                    new { value = "bob@example.com", type = "work", primary = true }
                }
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var user = body.RootElement.Deserialize<ScimUserWithEmails>();
        _ = user.ShouldNotBeNull();
        user.Emails.ShouldBeEquivalentTo(new[]
        {
            new ScimEmail { Value = "bob@example.com", Type = "work", Primary = true }
        });
    }

    [Fact]
    public async Task Create_user_with_full_rfc7643_schema()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (response, body) = await Fixture.Client.CreateUserAsync(
            "charlie",
            new
            {
                name = new { givenName = "Charlie", familyName = "Brown" },
                emails = new[]
                {
                    new { value = "charlie@example.com", type = "work", primary = true }
                },
                phoneNumbers = new[]
                {
                    new { value = "+1-555-555-0100", type = "work" }
                },
                addresses = new[]
                {
                    new
                    {
                        streetAddress = "100 Universal City Plaza",
                        locality = "Hollywood",
                        region = "CA",
                        postalCode = "91608",
                        country = "USA"
                    }
                },
                displayName = "Charlie Brown",
                active = true
            });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var user = body.RootElement.Deserialize<ScimUserFull>();
        _ = user.ShouldNotBeNull();
        user.Name.ShouldBeEquivalentTo(new ScimName { GivenName = "Charlie", FamilyName = "Brown" });
        user.Emails.ShouldBeEquivalentTo(new[]
        {
            new ScimEmail { Value = "charlie@example.com", Type = "work", Primary = true }
        });
        user.PhoneNumbers.ShouldBeEquivalentTo(new[]
        {
            new ScimPhoneNumber { Value = "+1-555-555-0100", Type = "work" }
        });
        user.Addresses.ShouldBeEquivalentTo(new[]
        {
            new ScimAddress
            {
                StreetAddress = "100 Universal City Plaza",
                Locality = "Hollywood",
                Region = "CA",
                PostalCode = "91608",
                Country = "USA"
            }
        });
        user.DisplayName.ShouldBe("Charlie Brown");
        user.Active.ShouldBe(true);
    }

    [Fact]
    public async Task Create_user_with_invalid_complex_subattribute_returns_400()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var payload = new
        {
            schemas = new[] { ScimHttpClient.UserSchemaUrn },
            userName = "dave",
            name = new { givenName = "Dave", bogusField = "bad" }
        };
        var response = await Fixture.Client.PostAsync(
            ScimHttpClient.UsersRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidValue");
    }

    [Fact]
    public async Task Create_user_with_wrong_type_for_complex_returns_400()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var payload = new
        {
            schemas = new[] { ScimHttpClient.UserSchemaUrn },
            userName = "eve",
            name = "not-an-object"
        };
        var response = await Fixture.Client.PostAsync(
            ScimHttpClient.UsersRoute, ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidValue");
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    private sealed record ScimUserWithName
    {
        [JsonPropertyName("name")]
        public ScimName? Name { get; init; }
    }

    private sealed record ScimUserWithEmails
    {
        [JsonPropertyName("emails")]
        public ScimEmail[]? Emails { get; init; }
    }

    private sealed record ScimUserFull
    {
        [JsonPropertyName("name")]
        public ScimName? Name { get; init; }

        [JsonPropertyName("emails")]
        public ScimEmail[]? Emails { get; init; }

        [JsonPropertyName("phoneNumbers")]
        public ScimPhoneNumber[]? PhoneNumbers { get; init; }

        [JsonPropertyName("addresses")]
        public ScimAddress[]? Addresses { get; init; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("active")]
        public bool? Active { get; init; }
    }

    private sealed record ScimName
    {
        [JsonPropertyName("givenName")]
        public string? GivenName { get; init; }

        [JsonPropertyName("familyName")]
        public string? FamilyName { get; init; }
    }

    private sealed record ScimEmail
    {
        [JsonPropertyName("value")]
        public string? Value { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("primary")]
        public bool? Primary { get; init; }
    }

    private sealed record ScimPhoneNumber
    {
        [JsonPropertyName("value")]
        public string? Value { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }
    }

    private sealed record ScimAddress
    {
        [JsonPropertyName("streetAddress")]
        public string? StreetAddress { get; init; }

        [JsonPropertyName("locality")]
        public string? Locality { get; init; }

        [JsonPropertyName("region")]
        public string? Region { get; init; }

        [JsonPropertyName("postalCode")]
        public string? PostalCode { get; init; }

        [JsonPropertyName("country")]
        public string? Country { get; init; }
    }
}
