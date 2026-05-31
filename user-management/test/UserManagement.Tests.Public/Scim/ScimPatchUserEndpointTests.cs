// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimPatchUserEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task Patch_replace_userName_returns_200()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("alice");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "replace", path = "userName", value = "alice-patched" }
            }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("userName").GetString().ShouldBe("alice-patched");
    }

    [Fact]
    public async Task Patch_add_externalId_returns_200()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("externalid", ScalarDataType.String, "SCIM externalId");

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("bob");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "add", path = "externalId", value = "ext-1" }
            }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("externalId").GetString().ShouldBe("ext-1");
    }

    [Fact]
    public async Task Patch_add_without_path_uses_value_object_keys()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("charlie");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new object[]
            {
                new { op = "add", value = new { userName = "charlie-new" } }
            }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("userName").GetString().ShouldBe("charlie-new");
    }

    [Fact]
    public async Task Patch_add_custom_attribute()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("department", ScalarDataType.String, "Department");

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("dave");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "add", path = "department", value = "Engineering" }
            }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("department").GetString().ShouldBe("Engineering");
    }

    [Fact]
    public async Task Patch_remove_externalId_returns_200()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterAttributeDefinitionAsync("externalid", ScalarDataType.String, "SCIM externalId");

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("eve", "ext-remove");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "remove", path = "externalId" }
            }
        };
        var patchResponse = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));
        patchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify via GET
        var getResponse = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await JsonDocument.ParseAsync(await getResponse.Content.ReadAsStreamAsync());
        body.RootElement.TryGetProperty("externalId", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Patch_remove_userName_returns_200()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("frank");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "remove", path = "userName" }
            }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_with_if_match_matching_returns_200()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("grace");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);
        var etag = createResponse.Headers.ETag!;

        var patchPayload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[] { new { op = "replace", path = "userName", value = "grace-v2" } }
        };
        var request = new HttpRequestMessage(HttpMethod.Patch, $"{ScimHttpClient.UsersRoute}/{id}")
        {
            Content = ScimHttpClient.ScimJsonContent(patchPayload)
        };
        request.Headers.IfMatch.Add(etag);
        var response = await Fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Patch_with_if_match_mismatched_returns_412()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("harry");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var patchPayload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[] { new { op = "replace", path = "userName", value = "harry-v2" } }
        };
        var request = new HttpRequestMessage(HttpMethod.Patch, $"{ScimHttpClient.UsersRoute}/{id}")
        {
            Content = ScimHttpClient.ScimJsonContent(patchPayload)
        };
        request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"99999\"", isWeak: true));
        var response = await Fixture.Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.PreconditionFailed);
    }

    [Fact]
    public async Task Patch_returns_new_etag()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("ivan");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);
        var originalEtag = ScimHttpClient.GetETag(createResponse);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[] { new { op = "replace", path = "userName", value = "ivan-v2" } }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var newEtag = ScimHttpClient.GetETag(response);
        newEtag.ShouldNotBe(originalEtag);
    }

    [Fact]
    public async Task Patch_nonexistent_user_returns_404()
    {
        await Fixture.InitializeAsync();

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[] { new { op = "replace", path = "userName", value = "x" } }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{Guid.NewGuid()}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_with_invalid_id_returns_404()
    {
        await Fixture.InitializeAsync();

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[] { new { op = "replace", path = "userName", value = "x" } }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/not-a-guid", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Patch_with_null_body_returns_400()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("jane");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var content = new StringContent("", Encoding.UTF8, ScimHttpClient.ScimContentType);
        var response = await Fixture.Client.PatchAsync($"{ScimHttpClient.UsersRoute}/{id}", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidSyntax");
    }

    [Fact]
    public async Task Patch_with_wrong_schema_returns_400()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("kate");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { "urn:wrong:schema" },
            Operations = new[] { new { op = "replace", path = "userName", value = "kate-v2" } }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidSyntax");
    }

    [Fact]
    public async Task Patch_with_unsupported_op_returns_400()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("leo");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[] { new { op = "unsupported", path = "userName", value = "leo-v2" } }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidValue");
    }

    [Fact]
    public async Task Patch_remove_without_path_returns_400()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("mia");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        // Serialize manually to avoid anonymous type including path=null
        var json = $"{{\"schemas\":[\"{ScimHttpClient.PatchOpSchemaUrn}\"],\"Operations\":[{{\"op\":\"remove\"}}]}}";
        var content = new StringContent(json, Encoding.UTF8, ScimHttpClient.ScimContentType);
        var response = await Fixture.Client.PatchAsync($"{ScimHttpClient.UsersRoute}/{id}", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("noTarget");
    }

    [Fact]
    public async Task Patch_with_complex_filter_path_returns_400()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("noah");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[] { new { op = "replace", path = "emails[type eq \"work\"].value", value = "work@example.com" } }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidPath");
    }

    [Fact]
    public async Task Patch_read_only_attribute_returns_400_mutability()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("olivia");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[] { new { op = "replace", path = "id", value = "new-id" } }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("mutability");
    }

    [Fact]
    public async Task Patch_add_without_path_and_non_object_value_returns_400()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("peter");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        // value is a string, not an object — must use raw JSON to avoid anonymous type always being object
        var json = $"{{\"schemas\":[\"{ScimHttpClient.PatchOpSchemaUrn}\"],\"Operations\":[{{\"op\":\"add\",\"value\":\"stringvalue\"}}]}}";
        var content = new StringContent(json, Encoding.UTF8, ScimHttpClient.ScimContentType);
        var response = await Fixture.Client.PatchAsync($"{ScimHttpClient.UsersRoute}/{id}", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidValue");
    }

    [Fact]
    public async Task Patch_duplicate_userName_returns_409()
    {
        await Fixture.InitializeAsync();
        var (r1, _) = await Fixture.Client.CreateUserAsync("alice");
        var (r2, b2) = await Fixture.Client.CreateUserAsync("bob");
        r1.StatusCode.ShouldBe(HttpStatusCode.Created);
        r2.StatusCode.ShouldBe(HttpStatusCode.Created);
        var bobId = ScimHttpClient.GetUserId(b2);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[] { new { op = "replace", path = "userName", value = "alice" } }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{bobId}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("uniqueness");
    }

    [Fact]
    public async Task Patch_response_content_type_is_scim_json()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("quinn");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[] { new { op = "replace", path = "userName", value = "quinn-v2" } }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(ScimHttpClient.ScimContentType);
    }

    [Fact]
    public async Task Patch_replace_complex_subattribute_via_dot_notation()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync(
            "alice",
            new
            {
                name = new { givenName = "Alice", familyName = "Smith" }
            });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "replace", path = "name.givenName", value = "Bob" }
            }
        };
        var patchResponse = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        patchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await patchResponse.Content.ReadFromJsonAsync<ScimUserWithName>();
        _ = user.ShouldNotBeNull();
        user.Name.ShouldBeEquivalentTo(new ScimName { GivenName = "Bob", FamilyName = "Smith" });
    }

    [Fact]
    public async Task Patch_add_complex_subattribute_creates_parent()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        // Create user WITHOUT a name attribute
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("charlie");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "add", path = "name.givenName", value = "Charlie" }
            }
        };
        var patchResponse = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        patchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await patchResponse.Content.ReadFromJsonAsync<ScimUserWithName>();
        _ = user.ShouldNotBeNull();
        user.Name.ShouldBeEquivalentTo(new ScimName { GivenName = "Charlie" });
    }

    [Fact]
    public async Task Patch_remove_complex_subattribute()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync(
            "dave",
            new
            {
                name = new { givenName = "Dave", familyName = "Jones" }
            });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new object[]
            {
                new { op = "remove", path = "name.givenName" }
            }
        };
        var patchResponse = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        patchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Verify via GET: name still exists with only familyname
        var getResponse = await Fixture.Client.GetAsync($"{ScimHttpClient.UsersRoute}/{id}");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await getResponse.Content.ReadFromJsonAsync<ScimUserWithName>();
        _ = user.ShouldNotBeNull();
        user.Name.ShouldBeEquivalentTo(new ScimName { FamilyName = "Jones" });
    }

    [Fact]
    public async Task Patch_replace_entire_complex_attribute()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync(
            "eve",
            new
            {
                name = new { givenName = "Old", familyName = "Name" }
            });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new object[]
            {
                new { op = "replace", path = "name", value = new { givenName = "New", familyName = "Name" } }
            }
        };
        var patchResponse = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        patchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await patchResponse.Content.ReadFromJsonAsync<ScimUserWithName>();
        _ = user.ShouldNotBeNull();
        user.Name.ShouldBeEquivalentTo(new ScimName { GivenName = "New", FamilyName = "Name" });
    }

    [Fact]
    public async Task Patch_add_entire_list_attribute()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("frank");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new object[]
            {
                new { op = "add", path = "emails", value = new[] { new { value = "frank@example.com", type = "work" } } }
            }
        };
        var patchResponse = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        patchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await patchResponse.Content.ReadFromJsonAsync<ScimUserWithEmails>();
        _ = user.ShouldNotBeNull();
        user.Emails.ShouldBeEquivalentTo(new[]
        {
            new ScimEmail { Value = "frank@example.com", Type = "work" }
        });
    }

    [Fact]
    public async Task Patch_dot_notation_invalid_subattribute_returns_400()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("grace");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "replace", path = "name.bogusField", value = "x" }
            }
        };
        var patchResponse = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        patchResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await patchResponse.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidPath");
    }

    [Fact]
    public async Task Patch_dot_notation_on_scalar_attribute_returns_400()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("hannah");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        // displayname is a scalar string — dot-notation should fail
        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "replace", path = "displayName.sub", value = "x" }
            }
        };
        var patchResponse = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        patchResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = await JsonDocument.ParseAsync(await patchResponse.Content.ReadAsStreamAsync());
        body.RootElement.GetProperty("scimType").GetString().ShouldBe("invalidPath");
    }

    [Fact]
    public async Task Patch_add_complex_subattribute_merges_with_existing()
    {
        await Fixture.InitializeAsync();
        await Fixture.RegisterScimUserSchemaAsync();

        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync(
            "iris",
            new
            {
                name = new { givenName = "Iris", familyName = "Wong" }
            });
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        // add op on entire complex should merge (not replace)
        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new object[]
            {
                new { op = "add", path = "name", value = new { givenName = "Updated" } }
            }
        };
        var patchResponse = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        patchResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var user = await patchResponse.Content.ReadFromJsonAsync<ScimUserWithName>();
        _ = user.ShouldNotBeNull();
        // givenname updated, familyname preserved due to merge semantics
        user.Name.ShouldBeEquivalentTo(new ScimName { GivenName = "Updated", FamilyName = "Wong" });
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

    private sealed record ScimName
    {
        [JsonPropertyName("givenName")]
        public string? GivenName { get; init; }

        [JsonPropertyName("familyName")]
        public string? FamilyName { get; init; }
    }

    [Fact]
    public async Task Patch_replace_userName_with_whitespace_returns_400()
    {
        await Fixture.InitializeAsync();
        var (createResponse, createBody) = await Fixture.Client.CreateUserAsync("whitespace-patch");
        createResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = ScimHttpClient.GetUserId(createBody);

        var payload = new
        {
            schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
            Operations = new[]
            {
                new { op = "replace", path = "userName", value = "   " }
            }
        };
        var response = await Fixture.Client.PatchAsync(
            $"{ScimHttpClient.UsersRoute}/{id}", ScimHttpClient.ScimJsonContent(payload));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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
}
