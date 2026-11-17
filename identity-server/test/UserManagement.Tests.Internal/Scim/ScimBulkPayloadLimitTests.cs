// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IO.Pipelines;
using Duende.UserManagement.Scim;
using Duende.UserManagement.Scim.Internal.Endpoints.Bulk;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement.Scim;

public sealed class ScimBulkPayloadLimitTests
{
    [Fact]
    public async Task should_restore_the_original_request_features_after_success()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .Configure<ScimOptions>(options => options.MaxBulkPayloadSize = 3)
            .BuildServiceProvider();

        await using var originalBody = new MemoryStream([1, 2, 3]);
        var originalBodyPipeFeature = new TestRequestBodyPipeFeature(originalBody);
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };

        context.Request.Body = originalBody;
        context.Features.Set<IRequestBodyPipeFeature>(originalBodyPipeFeature);
        var nextCalled = false;

        await ScimBulkPayloadLimit.EnforceAsync(
            context,
            async limitedContext =>
            {
                nextCalled = true;
                var bytes = new byte[3];
                await limitedContext.Request.Body.ReadExactlyAsync(
                    bytes,
                    TestContext.Current.CancellationToken);
            });

        nextCalled.ShouldBeTrue();
        context.Request.Body.ShouldBeSameAs(originalBody);
        context.Features.Get<IRequestBodyPipeFeature>().ShouldBeSameAs(originalBodyPipeFeature);
    }

    [Fact]
    public async Task should_limit_body_reader_and_restore_the_original_request_features()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .Configure<ScimOptions>(options => options.MaxBulkPayloadSize = 3)
            .BuildServiceProvider();

        await using var originalBody = new MemoryStream([1, 2, 3, 4]);
        var originalBodyPipeFeature = new TestRequestBodyPipeFeature(originalBody);
        var context = new DefaultHttpContext
        {
            RequestServices = services
        };

        context.Request.Body = originalBody;
        context.Features.Set<IRequestBodyPipeFeature>(originalBodyPipeFeature);
        context.Response.Body = new MemoryStream();

        await ScimBulkPayloadLimit.EnforceAsync(
            context,
            async limitedContext =>
            {
                while (true)
                {
                    var result = await limitedContext.Request.BodyReader.ReadAsync(
                        TestContext.Current.CancellationToken);

                    limitedContext.Request.BodyReader.AdvanceTo(result.Buffer.End);
                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            });

        context.Response.StatusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);
        context.Request.Body.ShouldBeSameAs(originalBody);
        context.Features.Get<IRequestBodyPipeFeature>().ShouldBeSameAs(originalBodyPipeFeature);
    }

    private sealed class TestRequestBodyPipeFeature(Stream stream) : IRequestBodyPipeFeature
    {
        public PipeReader Reader { get; } = PipeReader.Create(stream);
    }
}
