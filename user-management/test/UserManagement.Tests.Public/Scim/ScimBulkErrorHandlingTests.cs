// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimBulkErrorHandlingTests(ITestOutputHelper output, WebServerFixture serverFixture)
    : IAsyncDisposable
{
    private readonly ScimFixture Fixture = new(output, serverFixture);

    [Fact]
    public async Task failOnErrors_1_first_op_fails_second_is_skipped()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            failOnErrors = 1,
            Operations = new object[]
            {
                new
                {
                    method = "POST",
                    path = "/Users",
                    bulkId = "bad",
                    // Missing required userName — should fail with 400
                    data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn } }
                },
                new
                {
                    method = "POST",
                    path = "/Users",
                    bulkId = "good",
                    data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bulk-err-skip" }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ops = body.RootElement.GetProperty("Operations");
        ops.GetArrayLength().ShouldBe(2);

        // First op must be an error (4xx)
        var firstStatus = ops[0].GetProperty("status").GetString()!;
        int.Parse(firstStatus).ShouldBeGreaterThanOrEqualTo(400);

        // Second op must be skipped
        ops[1].GetProperty("status").GetString().ShouldBe("skipped");
    }

    [Fact]
    public async Task failOnErrors_2_two_errors_then_third_is_skipped()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            failOnErrors = 2,
            Operations = new object[]
            {
                new { method = "POST", path = "/Users", bulkId = "b1", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn } } },
                new { method = "POST", path = "/Users", bulkId = "b2", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn } } },
                new { method = "POST", path = "/Users", bulkId = "b3", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bulk-err-ok" } }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ops = body.RootElement.GetProperty("Operations");
        ops.GetArrayLength().ShouldBe(3);

        int.Parse(ops[0].GetProperty("status").GetString()!).ShouldBeGreaterThanOrEqualTo(400);
        int.Parse(ops[1].GetProperty("status").GetString()!).ShouldBeGreaterThanOrEqualTo(400);
        ops[2].GetProperty("status").GetString().ShouldBe("skipped");
    }

    [Fact]
    public async Task without_failOnErrors_all_operations_are_processed()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            // No failOnErrors — all ops should be attempted regardless of failures
            Operations = new object[]
            {
                new { method = "POST", path = "/Users", bulkId = "b1", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn } } },
                new { method = "POST", path = "/Users", bulkId = "b2", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn } } },
                new { method = "POST", path = "/Users", bulkId = "b3", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "bulk-no-fail-ok" } }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var ops = body.RootElement.GetProperty("Operations");
        ops.GetArrayLength().ShouldBe(3);

        // First two fail, but third must be processed (not skipped)
        int.Parse(ops[0].GetProperty("status").GetString()!).ShouldBeGreaterThanOrEqualTo(400);
        int.Parse(ops[1].GetProperty("status").GetString()!).ShouldBeGreaterThanOrEqualTo(400);
        ops[2].GetProperty("status").GetString().ShouldBe("201");
    }

    [Fact]
    public async Task exceeding_maxOperations_returns_413()
    {
        var fixture = new ScimFixture(output, serverFixture)
        {
            ConfigureScimCapabilities = o => o.MaxBulkOperations = 2
        };
        await fixture.InitializeAsync();

        var (response, _) = await fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new { method = "POST", path = "/Users", bulkId = "u1", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "limit-a" } },
                new { method = "POST", path = "/Users", bulkId = "u2", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "limit-b" } },
                new { method = "POST", path = "/Users", bulkId = "u3", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "limit-c" } }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);

        await fixture.DisposeAsync();
    }

    [Fact]
    public async Task exceeding_maxPayloadSize_returns_413()
    {
        var fixture = new ScimFixture(output, serverFixture)
        {
            ConfigureScimCapabilities = o => o.MaxBulkPayloadSize = 100
        };
        await fixture.InitializeAsync();

        // Build a payload clearly larger than 100 bytes
        var largeUserName = new string('x', 200);
        var (response, _) = await fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new { method = "POST", path = "/Users", bulkId = "u1", data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = largeUserName } }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);

        await fixture.DisposeAsync();
    }

    [Fact]
    public async Task unsupported_method_GET_returns_error_for_that_operation()
    {
        await Fixture.InitializeAsync();

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new { method = "GET", path = "/Users" }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var op = body.RootElement.GetProperty("Operations")[0];
        int.Parse(op.GetProperty("status").GetString()!).ShouldBeGreaterThanOrEqualTo(400);
    }

    [Fact]
    public async Task invalid_path_returns_error_for_that_operation()
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
                    path = "/UnknownResource",
                    bulkId = "bad",
                    data = new { schemas = new[] { ScimHttpClient.UserSchemaUrn }, userName = "x" }
                }
            }
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var op = body.RootElement.GetProperty("Operations")[0];
        int.Parse(op.GetProperty("status").GetString()!).ShouldBeGreaterThanOrEqualTo(400);
    }

    [Fact]
    public async Task delete_nonexistent_user_returns_404_per_operation()
    {
        await Fixture.InitializeAsync();

        var nonexistentId = Guid.NewGuid().ToString();

        var (response, body) = await Fixture.Client.BulkAsync(new
        {
            schemas = new[] { ScimHttpClient.BulkRequestSchemaUrn },
            Operations = new object[]
            {
                new { method = "DELETE", path = $"/Users/{nonexistentId}" }
            }
        });

        // Overall response is still 200 — per-op errors don't fail the bulk request
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var op = body.RootElement.GetProperty("Operations")[0];
        op.GetProperty("status").GetString().ShouldBe("404");
    }

    public async ValueTask DisposeAsync()
    {
        await Fixture.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
