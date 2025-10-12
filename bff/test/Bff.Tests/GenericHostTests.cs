// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.Bff.Tests.TestFramework;
using Microsoft.AspNetCore.Builder;

namespace Duende.Bff.Tests;

public class GenericHostTests(ITestOutputHelper output)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Test1()
    {
        var host = new GenericHost(output.WriteLine);
        host.OnConfigure += app => app.Run(ctx =>
        {
            ctx.Response.StatusCode = 204;
            return Task.CompletedTask;
        });
        await host.InitializeAsync();

        var response = await host.HttpClient.GetAsync("/test", _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
