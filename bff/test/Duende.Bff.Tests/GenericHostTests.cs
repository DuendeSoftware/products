// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Tests.TestFramework;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Duende.Bff.Tests
{
    public class GenericHostTests
    {
        [Fact]
        public async Task Test1()
        {
            var host = new GenericHost();
            host.OnConfigure += app => app.Run(ctx => {
                ctx.Response.StatusCode = 204;
                return Task.CompletedTask;
            });
            await host.InitializeAsync();

            var response = await host.HttpClient.GetAsync("/test");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }
}
