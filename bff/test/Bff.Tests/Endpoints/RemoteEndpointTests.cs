// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.Bff.DynamicFrontends;
using Duende.Bff.Tests.TestFramework;
using Duende.Bff.Tests.TestInfra;
using Duende.Bff.Yarp;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit.Abstractions;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;
using Resource = Duende.Bff.AccessTokenManagement.Resource;

namespace Duende.Bff.Tests.Endpoints;

public class RemoteEndpointTests : BffTestBase
{
    public RemoteEndpointTests(ITestOutputHelper output) : base(output)
    {
        Bff.OnConfigureServices += services =>
        {
            // Add a custom default transform that adds a header to the request
            services.AddSingleton<BffYarpTransformBuilder>(CustomDefaultBffTransformBuilder);

        };

        Bff.OnConfigureBff += bff => bff.AddRemoteApis();
    }

    private void CustomDefaultBffTransformBuilder(string localpath, TransformBuilderContext context)
    {
        context.AddResponseHeader("added-by-custom-default-transform", "some-value");
        DefaultBffYarpTransformerBuilders.DirectProxyWithAccessToken(localpath, context);
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task unauthenticated_calls_to_remote_endpoint_should_return_401(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken();
        };
        ConfigureBff(setup);
        await InitializeAsync();

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory]
    [MemberData(nameof(AllSetups))]
    public async Task calls_to_remote_endpoint_should_forward_user_to_api(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken();
        };
        ConfigureBff(setup);

        await InitializeAsync();
        await Bff.BrowserClient.Login();

        var (response, apiResult) = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path)
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.Path);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);

        response.Headers.GetValues("added-by-custom-default-transform").ShouldBe(["some-value"],
            "this value is added by the CustomDefaultBffTransformBuilder()");
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task
        calls_to_remote_endpoint_with_useraccesstokenparameters_having_stored_named_token_should_forward_user_to_api(
            BffSetupType setup)
    {
        var scheme = setup switch
        {
            BffSetupType.BffWithFrontend => Some.BffFrontend().CookieSchemeName,
            BffSetupType.V4Bff => BffAuthenticationSchemes.BffCookie,
            _ => Scheme.Parse("cookie")
        };

        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithUserAccessTokenParameter(new BffUserAccessTokenParameters
                {
                    SignInScheme = scheme,
                    ForceRenewal = true,
                    Resource = Resource.Parse("named_token_stored")
                })
                .WithAccessToken();
        };

        ConfigureBff(setup, configureOpenIdConnect: options =>
        {
            options.Events.OnUserInformationReceived = context =>
            {
                var tokens = new List<AuthenticationToken>();
                tokens.Add(new AuthenticationToken
                {
                    Name = $"{OpenIdConnectParameterNames.AccessToken}::named_token_stored",
                    Value = context.ProtocolMessage.AccessToken,
                });
                tokens.Add(new AuthenticationToken
                { Name = $"{OpenIdConnectParameterNames.TokenType}::named_token_stored", Value = "Bearer", });

                context.Properties!.StoreTokens(tokens);

                return Task.CompletedTask;
            };
            The.DefaultOpenIdConnectConfiguration(options);
        });
        await InitializeAsync();
        await Bff.BrowserClient.Login();

        var (response, apiResult) = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path)
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.Path);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task
        calls_to_remote_endpoint_with_useraccesstokenparameters_having_not_stored_corresponding_named_token_finds_no_matching_token_should_fail(
            BffSetupType setup)
    {
        var scheme = setup switch
        {
            BffSetupType.BffWithFrontend => Some.BffFrontend().CookieSchemeName,
            BffSetupType.V4Bff => BffAuthenticationSchemes.BffCookie,
            _ => Scheme.Parse("cookie")
        };

        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithUserAccessTokenParameter(new BffUserAccessTokenParameters
                {
                    SignInScheme = scheme,
                    ForceRenewal = true,
                    Resource = Resource.Parse("should_not_be_found")
                })
                .WithAccessToken();
        };

        ConfigureBff(setup, configureOpenIdConnect: options =>
        {
            options.Events.OnUserInformationReceived = context =>
            {
                var tokens = new List<AuthenticationToken>();
                tokens.Add(new AuthenticationToken
                {
                    Name = $"{OpenIdConnectParameterNames.AccessToken}::named_token_stored",
                    Value = context.ProtocolMessage.AccessToken,
                });
                tokens.Add(new AuthenticationToken
                { Name = $"{OpenIdConnectParameterNames.TokenType}::named_token_stored", Value = "Bearer", });

                context.Properties!.StoreTokens(tokens);

                return Task.CompletedTask;
            };
            The.DefaultOpenIdConnectConfiguration(options);
        });
        await InitializeAsync();
        await Bff.BrowserClient.Login();

        var (response, apiResult) = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task put_to_remote_endpoint_should_forward_user_to_api(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken();
        };
        ConfigureBff(setup);

        await InitializeAsync();
        await Bff.BrowserClient.Login();
        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            method: HttpMethod.Put,
            content: JsonContent.Create(new TestPayload("hello test api"))
        );

        apiResult.Method.ShouldBe(HttpMethod.Put);
        apiResult.Path.ShouldBe(The.Path);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task post_to_remote_endpoint_should_forward_user_to_api(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken();
        };
        ConfigureBff(setup);

        await InitializeAsync();
        await Bff.BrowserClient.Login();
        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            method: HttpMethod.Post,
            content: JsonContent.Create(new TestPayload("hello test api"))
        );

        apiResult.Method.ShouldBe(HttpMethod.Post);
        apiResult.Path.ShouldBe(The.Path);
        apiResult.Sub.ShouldBe(The.Sub);
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task calls_to_remote_endpoint_should_forward_user_or_anonymous_to_api(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken(RequiredTokenType.UserOrNone);
        };
        ConfigureBff(setup);

        await InitializeAsync();
        {
            ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
                url: Bff.Url(The.Path)
            );

            apiResult.Method.ShouldBe(HttpMethod.Get);
            apiResult.Path.ShouldBe(The.Path);
            apiResult.Sub.ShouldBeNull();
            apiResult.ClientId.ShouldBeNull();
        }

        {
            await Bff.BrowserClient.Login();

            ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
                url: Bff.Url(The.Path)
            );

            apiResult.Method.ShouldBe(HttpMethod.Get);
            apiResult.Path.ShouldBe(The.Path);

            apiResult.Sub.ShouldBe(The.Sub);
            apiResult.ClientId.ShouldBe(The.ClientId);
        }
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task calls_to_remote_endpoint_should_forward_client_token_to_api(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken(RequiredTokenType.Client);
        };
        ConfigureBff(setup);

        await InitializeAsync();
        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path)
        );

        apiResult.Method.ShouldBe(HttpMethod.Get);
        apiResult.Path.ShouldBe(The.Path);

        apiResult.Sub.ShouldBeNull();
        apiResult.ClientId.ShouldBe(The.ClientId);
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task calls_to_remote_endpoint_should_fail_when_token_retrieval_fails(BffSetupType setup)
    {
        Bff.OnConfigureServices += services =>
        {
            // Add a custom access token retriever that always fails
            services.AddSingleton<FailureAccessTokenRetriever>();
        };

        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessTokenRetriever<FailureAccessTokenRetriever>()
                .WithAccessToken(RequiredTokenType.UserOrClient);
        };
        ConfigureBff(setup);

        await InitializeAsync();
        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );

        // user should be signed out
        var result = await Bff.BrowserClient.GetIsUserLoggedInAsync();
        result.ShouldBeFalse();
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task calls_to_remote_api_that_returns_forbidden_will_return_forbidden(BffSetupType setup)
    {
        Api.ApiStatusCodeToReturn = HttpStatusCode.Forbidden;
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken(RequiredTokenType.None);
        };
        ConfigureBff(setup);
        await InitializeAsync();

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Forbidden
        );
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task calls_to_remote_api_that_returns_unauthorized_will_return_unauthorized(BffSetupType setup)
    {
        Api.ApiStatusCodeToReturn = HttpStatusCode.Unauthorized;
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken(RequiredTokenType.None);
        };
        ConfigureBff(setup);
        await InitializeAsync();

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task calls_to_remote_endpoint_should_send_token_from_token_retriever_when_token_retrieval_succeeds(
        BffSetupType setup)
    {
        Bff.OnConfigureServices += services =>
        {
            // Add a custom access token retriever that always fails
            services.AddSingleton(new TestAccessTokenRetriever(() => CreateAccessToken("123", "fake-client")));
        };

        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessTokenRetriever<TestAccessTokenRetriever>()
                .WithAccessToken();
        };
        ConfigureBff(setup);
        await InitializeAsync();

        ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path)
        );

        apiResult.Sub.ShouldBe("123");
        apiResult.ClientId.ShouldBe("fake-client");
    }

    private async Task<AccessTokenResult> CreateAccessToken(string sub, string clientId)
    {
        var tokens = IdentityServer.Resolve<ITokenService>();
        var token = new Token(IdentityServerConstants.TokenTypes.AccessToken)
        {
            Issuer = IdentityServer.Url().ToString().TrimEnd('/'),
            Lifetime = Convert.ToInt32(TimeSpan.FromDays(1).TotalSeconds),
            CreationTime = DateTime.UtcNow,

            Claims = new List<Claim>
            {
                new("client_id", clientId),
                new("sub", sub)
            },
            Audiences = new List<string>
            {
                IdentityServer.Url("/resources").ToString()
            },
            AccessTokenType = AccessTokenType.Jwt
        };

        return new BearerTokenResult()
        {
            AccessToken = AccessToken.Parse(await tokens.CreateSecurityTokenAsync(token))
        };
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task calls_to_remote_endpoint_should_forward_user_or_client_to_api(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken(RequiredTokenType.UserOrClient);
        };
        ConfigureBff(setup);

        await InitializeAsync();
        {
            ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
                url: Bff.Url(The.Path)
            );

            apiResult.Method.ShouldBe(HttpMethod.Get);
            apiResult.Path.ShouldBe(The.Path);
            apiResult.Sub.ShouldBeNull();
            apiResult.ClientId.ShouldBe(The.ClientId);
        }

        {
            await Bff.BrowserClient.Login();

            ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
                url: Bff.Url(The.Path)
            );

            apiResult.Method.ShouldBe(HttpMethod.Get);
            apiResult.Path.ShouldBe(The.Path);

            apiResult.Sub.ShouldBe(The.Sub);
            apiResult.ClientId.ShouldBe(The.ClientId);
        }
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task calls_to_remote_endpoint_with_anon_should_be_anon(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken(RequiredTokenType.None);
        };
        ConfigureBff(setup);

        await InitializeAsync();
        {
            ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
                url: Bff.Url(The.Path)
            );

            apiResult.Method.ShouldBe(HttpMethod.Get);
            apiResult.Path.ShouldBe(The.Path);
            apiResult.Sub.ShouldBeNull();
            apiResult.ClientId.ShouldBeNull();
        }

        {
            await Bff.BrowserClient.Login();
            ApiCallDetails apiResult = await Bff.BrowserClient.CallBffHostApi(
                url: Bff.Url(The.Path)
            );

            apiResult.Method.ShouldBe(HttpMethod.Get);
            apiResult.Path.ShouldBe(The.Path);
            apiResult.Sub.ShouldBeNull();
            apiResult.ClientId.ShouldBeNull();
        }
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task when_id_srv_client_is_disabled_then_unauthorized(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint("/api_user_or_client", Api.Url())
                .WithAccessToken(RequiredTokenType.UserOrClient);

            app.MapRemoteBffApiEndpoint("/api_client", Api.Url())
                .WithAccessToken(RequiredTokenType.Client);

            app.MapRemoteBffApiEndpoint("/none", Api.Url())
                .WithAccessToken(RequiredTokenType.None);
        };
        ConfigureBff(setup);

        await InitializeAsync();
        foreach (var client in IdentityServer.Clients)
        {
            client.Enabled = false;
        }

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_user_or_client/test"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/api_client/test"),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url("/none"),
            expectedStatusCode: HttpStatusCode.OK
        );
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task response_status_401_from_remote_endpoint_should_return_401_from_bff(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken(RequiredTokenType.None);
        };
        ConfigureBff(setup);

        await InitializeAsync();
        await Bff.BrowserClient.Login();

        Api.ApiStatusCodeToReturn = HttpStatusCode.Unauthorized;

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Unauthorized
        );
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task response_status_403_from_remote_endpoint_should_return_403_from_bff(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken(RequiredTokenType.None);
        };
        ConfigureBff(setup);

        await InitializeAsync();
        await Bff.BrowserClient.Login();

        Api.ApiStatusCodeToReturn = HttpStatusCode.Forbidden;

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.Path),
            expectedStatusCode: HttpStatusCode.Forbidden
        );
    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task calls_to_remote_endpoint_should_require_csrf(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken(RequiredTokenType.None);
        };
        ConfigureBff(setup);

        await InitializeAsync();

        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url(The.Path));
        var response = await Bff.BrowserClient.SendAsync(req);
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized,
            "The endpoint requires CSRF protection, so it should return 403 Forbidden when no CSRF header is present.");

    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task endpoints_that_disable_csrf_should_not_require_csrf_header(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path))
                .WithAccessToken(RequiredTokenType.UserOrClient)
                .SkipAntiforgery();
        };
        ConfigureBff(setup);

        await InitializeAsync();
        await Bff.BrowserClient.Login();

        var req = new HttpRequestMessage(HttpMethod.Get, Bff.Url(The.Path));
        var response = await Bff.BrowserClient.SendAsync(req);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

    }

    [Theory, MemberData(nameof(AllSetups))]
    public async Task MapRemoteBffApiEndpoint_can_override_default_transform(BffSetupType setup)
    {
        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(The.Path, Api.Url(The.Path), c =>
                {
                    DefaultBffYarpTransformerBuilders.DirectProxyWithAccessToken(The.Path, c);
                    c.AddRequestHeader("custom", "with value");
                    // Add a transform that adds the catchall route value to a header
                    c.AddRequestTransform(async context =>
                    {
                        // One of our customers asked how to access the catch-all route value in subsequent transforms
                        if (context.HttpContext.Request.RouteValues.TryGetValue("catch-all", out var value) && value is string s)
                        {
                            context.ProxyRequest.Headers.Add("catch-all", "/" + s);
                        }
                        await Task.CompletedTask;
                    });
                })
                .WithAccessToken();
        };
        ConfigureBff(setup);

        await InitializeAsync();
        await Bff.BrowserClient.Login();

        ApiCallDetails result = await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.PathAndSubPath)
        );

        result.RequestHeaders.Keys.ShouldNotContain("added-by-custom-default-transform");
        result.RequestHeaders.TryGetValue("custom", out var customValue).ShouldBeTrue();
        customValue.ShouldBe(["with value"],
            "The custom header should be added by the custom transform registered in the test.");

        result.RequestHeaders.TryGetValue("catch-all", out var catchAll).ShouldBeTrue();
        catchAll.ShouldBe([The.SubPath],
            "The catch-all route value should be added to the request headers by the custom transform registered in the test.");
    }

    // now I don't like timeouts in tests, but I don't know of a better way to create http timeouts. 
    [Theory, MemberData(nameof(AllSetups))]
    public async Task Can_register_default_forwarder_config_for_MapRemoteBffApiEndpoint(BffSetupType setup)
    {
        var shouldDelay = false;

        Api.OnConfigure += app =>
        {
            app.Use(async (c, n) =>
            {
                if (shouldDelay)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
                await n();
            });
        };

        Bff.OnConfigureServices += services =>
        {
            // Add a default ForwarderRequestConfig that has a 100 ms timeout
            services.AddSingleton(new ForwarderRequestConfig()
            {
                ActivityTimeout = TimeSpan.FromMilliseconds(100)
            });
        };

        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(
                    localPath: The.Path,
                    apiAddress: Api.Url(The.Path))
                .WithAccessToken();
        };

        ConfigureBff(setup);
        await InitializeAsync();
        await Bff.BrowserClient.Login();

        // first, ensure that the 'normal' process works, becuase delay's are turned off. 
        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.PathAndSubPath)
        );

        // turn on delays. Now the timeout of 100 ms should kick in. 
        shouldDelay = true;

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.PathAndSubPath),
            expectedStatusCode: HttpStatusCode.BadGateway
        );
    }

    // now I don't like timeouts in tests, but I don't know of a better way to create http timeouts. 
    [Theory, MemberData(nameof(AllSetups))]
    public async Task MapRemoteBffApiEndpoint_can_override_config_to_configure_a_timeout(BffSetupType setup)
    {
        var shouldDelay = false;

        Api.OnConfigure += app =>
        {
            app.Use(async (c, n) =>
            {
                if (shouldDelay)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
                await n();
            });
        };

        Bff.OnConfigureEndpoints += app =>
        {
            app.MapRemoteBffApiEndpoint(
                    localPath: The.Path,
                    apiAddress: Api.Url(The.Path),
                    requestConfig: new ForwarderRequestConfig()
                    {
                        // 100 ms timeout, which is not too short that the normal process might fail,
                        // but not too long that the test will take forever
                        ActivityTimeout = TimeSpan.FromMilliseconds(100)
                    })
                .WithAccessToken();
        };

        ConfigureBff(setup);
        await InitializeAsync();
        await Bff.BrowserClient.Login();

        // first, ensure that the 'normal' process works, becuase delay's are turned off. 
        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.PathAndSubPath)
        );

        // turn on delays. Now the timeout of 100 ms should kick in. 
        shouldDelay = true;

        await Bff.BrowserClient.CallBffHostApi(
            url: Bff.Url(The.PathAndSubPath),
            expectedStatusCode: HttpStatusCode.BadGateway
        );
    }
}
