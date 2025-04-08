// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Internal;
using Duende.Bff.Tests.TestHosts;
using Duende.Bff.Yarp;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Duende.Bff.Tests;

/// <summary>
/// These tests prove that you can use a custom IAccessTokenRetriever and that the context is populated correctly. 
/// </summary>
public class IAccessTokenRetriever_Extensibility_tests : BffIntegrationTestBase
{
    private ContextCapturingAccessTokenRetriever _customAccessTokenReceiver { get; } = new(NullLogger<DefaultAccessTokenRetriever>.Instance);

    public IAccessTokenRetriever_Extensibility_tests(ITestOutputHelper output) : base(output)
    {
        Bff.OnConfigureServices += services =>
        {
            services.AddSingleton(_customAccessTokenReceiver);
        };

        Bff.OnConfigure += app =>
        {
            app.UseEndpoints((endpoints) =>
            {
                endpoints.MapRemoteBffApiEndpoint("/custom", Api.Url("/some/path"))
                    .WithAccessToken()
                    .WithAccessTokenRetriever<ContextCapturingAccessTokenRetriever>();

            });

            app.Map("/subPath",
                subPath =>
                {
                    subPath.UseRouting();
                    subPath.UseEndpoints((endpoints) =>
                    {
                        endpoints.MapRemoteBffApiEndpoint("/custom_within_subpath", Api.Url("/some/path"))
                            .WithAccessToken()
                            .WithAccessTokenRetriever<ContextCapturingAccessTokenRetriever>();
                    });
                });

        };
    }

    [Fact]
    public async Task When_calling_custom_endpoint_then_AccessTokenRetrievalContext_has_api_address_and_localpath()
    {
        await Bff.BffLoginAsync("alice");

        await Bff.BrowserClient.CallBffHostApi(Bff.Url("/custom"));

        var usedContext = _customAccessTokenReceiver.UsedContext.ShouldNotBeNull();

        usedContext.Metadata.TokenType.ShouldBe(RequiredTokenType.User);

        usedContext.ApiAddress.ShouldBe(new Uri(Api.Url("/some/path")));
        usedContext.LocalPath.ToString().ShouldBe("/custom");

    }

    [Fact]
    public async Task When_calling_sub_custom_endpoint_then_AccessTokenRetrievalContext_has_api_address_and_localpath()
    {
        await Bff.BffLoginAsync("alice");

        await Bff.BrowserClient.CallBffHostApi(Bff.Url("/subPath/custom_within_subpath"));

        var usedContext = _customAccessTokenReceiver.UsedContext.ShouldNotBeNull();

        usedContext.ApiAddress.ShouldBe(new Uri(Api.Url("/some/path")));
        usedContext.LocalPath.ToString().ShouldBe("/custom_within_subpath");

    }

    /// <summary>
    /// Captures the context in which the access token retriever is called, so we can assert on it
    /// </summary>
    private class ContextCapturingAccessTokenRetriever : IAccessTokenRetriever
    {
        public AccessTokenRetrievalContext? UsedContext { get; private set; }
        public ContextCapturingAccessTokenRetriever(ILogger<DefaultAccessTokenRetriever> logger) : base()
        {
        }

        public async Task<AccessTokenResult> GetAccessTokenAsync(AccessTokenRetrievalContext context, CT ct = default)
        {
            UsedContext = context;
            if (context.Metadata.TokenType.HasValue)
            {
                return await context.HttpContext.GetManagedAccessToken(
                    requiredTokenType: context.Metadata.TokenType.Value,
                    context.UserTokenRequestParameters, ct: ct);
            }
            else
            {
                return new NoAccessTokenResult();
            }
        }
    }

}
