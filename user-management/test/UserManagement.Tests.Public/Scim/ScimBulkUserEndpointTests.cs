// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimBulkUserEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private static readonly string[] WrongSchema = ["urn:wrong:schema"];

    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task bulk_create_user_returns_200_with_201_operation_status()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new[]
            {
                new
                {
                    method = "POST",
                    path = "/Users",
                    bulkId = "user1",
                    data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bulk-alice" }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ops = body.RootElement.GetProperty("Operations");
        ops.GetArrayLength().ShouldBe(1);

        var op = ops[0];
        op.GetProperty("status").GetString().ShouldBe("201");
        op.GetProperty("bulkId").GetString().ShouldBe("user1");
        ShouldlyExtensions.ShouldContain(op.GetProperty("location").GetString()!, "/scim/Users/");
        op.GetProperty("method").GetString().ShouldBe("POST");
    }

    [Fact]
    public async Task bulk_create_user_location_points_to_real_resource()
    {
        await Fixture.InitializeAsync();

        var (_, bulkBody) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new[]
            {
                new
                {
                    method = "POST",
                    path = "/Users",
                    bulkId = "u1",
                    data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bulk-bob" }
                }
            }
        });

        var location = bulkBody.RootElement.GetProperty("Operations")[0]
            .GetProperty("location").GetString()!;

        var getResponse = await Fixture.Client.GetAsync(location.Replace(Fixture.Client.BaseAddress!.ToString(), "/"));
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task bulk_create_multiple_users_all_succeed()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new[]
            {
                new { method = "POST", path = "/Users", bulkId = "u1", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bulk-carol" } },
                new { method = "POST", path = "/Users", bulkId = "u2", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bulk-dave" } }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ops = body.RootElement.GetProperty("Operations");
        ops.GetArrayLength().ShouldBe(2);
        ops[0].GetProperty("status").GetString().ShouldBe("201");
        ops[1].GetProperty("status").GetString().ShouldBe("201");
    }

    [Fact]
    public async Task bulk_replace_user_returns_200_operation_status()
    {
        await Fixture.InitializeAsync();

        var (_, createBody) = await Fixture.Client.CreateUserAsync("bulk-eve");
        var userId = ScimHttpClient.GetUserId(createBody);

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new[]
            {
                new
                {
                    method = "PUT",
                    path = $"/Users/{userId}",
                    data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bulk-eve-updated" }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.RootElement.GetProperty("Operations")[0].GetProperty("status").GetString().ShouldBe("200");
    }

    [Fact]
    public async Task bulk_patch_user_returns_200_operation_status()
    {
        await Fixture.InitializeAsync();

        var (_, createBody) = await Fixture.Client.CreateUserAsync("bulk-frank");
        var userId = ScimHttpClient.GetUserId(createBody);

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new[]
            {
                new
                {
                    method = "PATCH",
                    path = $"/Users/{userId}",
                    data = new
                    {
                        schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
                        Operations = new[]
                        {
                            new { op = "replace", path = "userName", value = "bulk-frank-patched" }
                        }
                    }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.RootElement.GetProperty("Operations")[0].GetProperty("status").GetString().ShouldBe("200");
    }

    [Fact]
    public async Task bulk_delete_user_returns_204_operation_status()
    {
        await Fixture.InitializeAsync();

        var (_, createBody) = await Fixture.Client.CreateUserAsync("bulk-grace");
        var userId = ScimHttpClient.GetUserId(createBody);

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new[]
            {
                new { method = "DELETE", path = $"/Users/{userId}" }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.RootElement.GetProperty("Operations")[0].GetProperty("status").GetString().ShouldBe("204");
    }

    [Fact]
    public async Task bulk_mixed_operations_all_succeed()
    {
        await Fixture.InitializeAsync();

        var (_, existingBody) = await Fixture.Client.CreateUserAsync("bulk-henry");
        var existingId = ScimHttpClient.GetUserId(existingBody);

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new { method = "POST", path = "/Users", bulkId = "new1", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bulk-iris" } },
                new { method = "PUT",  path = $"/Users/{existingId}", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bulk-henry-updated" } },
                new { method = "DELETE", path = $"/Users/{existingId}" }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ops = body.RootElement.GetProperty("Operations");
        ops[0].GetProperty("status").GetString().ShouldBe("201");
        ops[1].GetProperty("status").GetString().ShouldBe("200");
        ops[2].GetProperty("status").GetString().ShouldBe("204");
    }

    [Fact]
    public async Task null_body_returns_400()
    {
        await Fixture.InitializeAsync();

        var response = await Fixture.Client.PostAsync(ScimHttpClient.BulkRoute,
            ScimHttpClient.ScimJsonContent(new { }));

        // Minimal body with no Operations field — ASP.NET returns 400 for required binding failure
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task wrong_schema_returns_400()
    {
        await Fixture.InitializeAsync();

        var (response, _) = await Fixture.Client.BulkAsync(new
        {
            schemas = WrongSchema,
            Operations = new[]
            {
                new { method = "POST", path = "/Users", bulkId = "x", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "x" } }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task response_content_type_is_scim_json()
    {
        await Fixture.InitializeAsync();

        var (response, _) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new[]
            {
                new { method = "POST", path = "/Users", bulkId = "u1", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bulk-ct-test" } }
            }
        });

        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/scim+json");
    }

    [Fact]
    public async Task bulk_response_includes_correct_schema_urn()
    {
        await Fixture.InitializeAsync();

        var (_, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new[]
            {
                new { method = "POST", path = "/Users", bulkId = "u1", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bulk-schema-test" } }
            }
        });

        body.RootElement.GetProperty("schemas").EnumerateArray()
            .Select(e => e.GetString())
            .ShouldContain(ScimHttpClient.BulkResponseSchemaUrn);
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
