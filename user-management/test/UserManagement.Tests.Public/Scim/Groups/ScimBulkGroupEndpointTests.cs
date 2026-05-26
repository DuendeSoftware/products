// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim.Groups;

public sealed class ScimBulkGroupEndpointTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task bulk_create_group_returns_201_operation_status()
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
                    path = "/Groups",
                    bulkId = "g1",
                    data = new { schemas = new[] { ScimGroupHttpClient.GroupSchemaUrn }, displayName = "Bulk Group Alpha" }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ops = body.RootElement.GetProperty("Operations");
        ops.GetArrayLength().ShouldBe(1);

        var op = ops[0];
        op.GetProperty("status").GetString().ShouldBe("201");
        op.GetProperty("bulkId").GetString().ShouldBe("g1");
        ShouldlyExtensions.ShouldContain(op.GetProperty("location").GetString()!, "/scim/Groups/");
        op.GetProperty("method").GetString().ShouldBe("POST");
    }

    [Fact]
    public async Task bulk_replace_group_returns_200_operation_status()
    {
        await Fixture.InitializeAsync();

        var (_, createBody) = await Fixture.GroupClient.CreateGroupAsync("Bulk Group Beta");
        var groupId = ScimGroupHttpClient.GetGroupId(createBody);

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new
                {
                    method = "PUT",
                    path = $"/Groups/{groupId}",
                    data = new { schemas = new[] { ScimGroupHttpClient.GroupSchemaUrn }, displayName = "Bulk Group Beta Updated" }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.RootElement.GetProperty("Operations")[0].GetProperty("status").GetString().ShouldBe("200");
    }

    [Fact]
    public async Task bulk_patch_group_returns_200_operation_status()
    {
        await Fixture.InitializeAsync();

        var (_, createBody) = await Fixture.GroupClient.CreateGroupAsync("Bulk Group Gamma");
        var groupId = ScimGroupHttpClient.GetGroupId(createBody);

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new
                {
                    method = "PATCH",
                    path = $"/Groups/{groupId}",
                    data = new
                    {
                        schemas = new[] { ScimHttpClient.PatchOpSchemaUrn },
                        Operations = new[]
                        {
                            new { op = "replace", path = "displayName", value = "Bulk Group Gamma Patched" }
                        }
                    }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.RootElement.GetProperty("Operations")[0].GetProperty("status").GetString().ShouldBe("200");
    }

    [Fact]
    public async Task bulk_delete_group_returns_204_operation_status()
    {
        await Fixture.InitializeAsync();

        var (_, createBody) = await Fixture.GroupClient.CreateGroupAsync("Bulk Group Delta");
        var groupId = ScimGroupHttpClient.GetGroupId(createBody);

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new { method = "DELETE", path = $"/Groups/{groupId}" }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        body.RootElement.GetProperty("Operations")[0].GetProperty("status").GetString().ShouldBe("204");
    }

    [Fact]
    public async Task bulk_mixed_users_and_groups_all_succeed()
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
                    data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bulk-mixed-user" }
                },
                new
                {
                    method = "POST",
                    path = "/Groups",
                    bulkId = "g1",
                    data = new { schemas = new[] { ScimGroupHttpClient.GroupSchemaUrn }, displayName = "Bulk Mixed Group" }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ops = body.RootElement.GetProperty("Operations");
        ops.GetArrayLength().ShouldBe(2);
        ops[0].GetProperty("status").GetString().ShouldBe("201");
        ops[1].GetProperty("status").GetString().ShouldBe("201");

        ShouldlyExtensions.ShouldContain(ops[0].GetProperty("location").GetString()!, "/scim/Users/");
        ShouldlyExtensions.ShouldContain(ops[1].GetProperty("location").GetString()!, "/scim/Groups/");
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
