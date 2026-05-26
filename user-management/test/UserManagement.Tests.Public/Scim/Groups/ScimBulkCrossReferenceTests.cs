// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Text.Json;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim.Groups;

public sealed class ScimBulkCrossReferenceTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task create_user_then_patch_via_bulkId_path()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new
                {
                    method = "POST",
                    path = "/Users",
                    bulkId = "u1",
                    data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "cross-alice" }
                },
                new
                {
                    method = "PATCH",
                    path = "/Users/bulkId:u1",
                    data = new
                    {
                        schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
                        Operations = new[] { new { op = "replace", path = "userName", value = "cross-alice-patched" } }
                    }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ops = body.RootElement.GetProperty("Operations");
        ops[0].GetProperty("status").GetString().ShouldBe("201");
        ops[1].GetProperty("status").GetString().ShouldBe("200");
    }

    [Fact]
    public async Task create_user_then_replace_via_bulkId_path()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new
                {
                    method = "POST",
                    path = "/Users",
                    bulkId = "u1",
                    data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "cross-bob" }
                },
                new
                {
                    method = "PUT",
                    path = "/Users/bulkId:u1",
                    data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "cross-bob-replaced" }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ops = body.RootElement.GetProperty("Operations");
        ops[0].GetProperty("status").GetString().ShouldBe("201");
        ops[1].GetProperty("status").GetString().ShouldBe("200");
    }

    [Fact]
    public async Task create_user_then_delete_via_bulkId_path()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new
                {
                    method = "POST",
                    path = "/Users",
                    bulkId = "u1",
                    data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "cross-carol" }
                },
                new
                {
                    method = "DELETE",
                    path = "/Users/bulkId:u1"
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ops = body.RootElement.GetProperty("Operations");
        ops[0].GetProperty("status").GetString().ShouldBe("201");
        ops[1].GetProperty("status").GetString().ShouldBe("204");
    }

    [Fact]
    public async Task create_user_then_add_to_group_via_bulkId_in_data()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new
                {
                    method = "POST",
                    path = "/Users",
                    bulkId = "u1",
                    data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "cross-dave" }
                },
                new
                {
                    method = "POST",
                    path = "/Groups",
                    bulkId = "g1",
                    data = new
                    {
                        schemas = new[] { ScimGroupHttpClient.GroupSchemaUrn },
                        displayName = "Cross-Ref Group",
                        members = new[] { new { value = "bulkId:u1" } }
                    }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ops = body.RootElement.GetProperty("Operations");
        ops[0].GetProperty("status").GetString().ShouldBe("201");
        ops[1].GetProperty("status").GetString().ShouldBe("201");

        // Verify the group contains the created user's ID
        var userId = ops[0].GetProperty("location").GetString()!.Split('/')[^1];
        var groupId = ops[1].GetProperty("location").GetString()!.Split('/')[^1];

        var groupResponse = await Fixture.Client.GetAsync($"/scim/Groups/{groupId}");
        groupResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var groupBody = JsonDocument.Parse(await groupResponse.Content.ReadAsStringAsync());
        var members = groupBody.RootElement.GetProperty("members");
        members.EnumerateArray()
            .Select(m => m.GetProperty("value").GetString())
            .ShouldContain(userId);
    }

    [Fact]
    public async Task unresolved_bulkId_in_path_returns_409_for_that_operation()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new
                {
                    method = "PATCH",
                    path = "/Users/bulkId:nonexistent",
                    data = new
                    {
                        schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
                        Operations = new[] { new { op = "replace", path = "userName", value = "x" } }
                    }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var op = body.RootElement.GetProperty("Operations")[0];
        op.GetProperty("status").GetString().ShouldBe("409");
    }

    [Fact]
    public async Task forward_reference_bulkId_returns_409_for_first_op_then_continues()
    {
        await Fixture.InitializeAsync();

        // Operation 1 references a bulkId that only gets defined in Operation 2
        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new
                {
                    method = "PATCH",
                    path = "/Users/bulkId:later",  // "later" hasn't been registered yet
                    data = new
                    {
                        schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
                        Operations = new[] { new { op = "replace", path = "userName", value = "x" } }
                    }
                },
                new
                {
                    method = "POST",
                    path = "/Users",
                    bulkId = "later",
                    data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "cross-eve" }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ops = body.RootElement.GetProperty("Operations");
        // First op fails (forward reference)
        ops[0].GetProperty("status").GetString().ShouldBe("409");
        // Second op still processes (no failOnErrors set)
        ops[1].GetProperty("status").GetString().ShouldBe("201");
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
