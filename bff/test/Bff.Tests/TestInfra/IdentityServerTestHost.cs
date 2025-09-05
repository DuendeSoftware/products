// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using System.Security.Claims;
using Duende.Bff.DynamicFrontends;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace Duende.Bff.Tests.TestInfra;

public class IdentityServerTestHost : TestHost
{
    public IdentityServerTestHost(TestHostContext context) : base(context, new Uri("https://identity-server"))
    {
        UserToSignIn = Some.ClaimsPrincipal();

        OnConfigureServices += services =>
        {
            if (!ApiScopes.Any())
            {
                ApiScopes.Add(new ApiScope(The.Scope));
            }

            services.AddHttpClient(IdentityServerConstants.HttpClients.BackChannelLogoutHttpClient)
                .ConfigurePrimaryHttpMessageHandler(() => context.Internet);

            var idsrv = services.AddIdentityServer(options =>
                {
                    options.EmitStaticAudienceClaim = true;
                    options.UserInteraction.CreateAccountUrl = "/account/create";
                })
                .AddInMemoryClients(Clients)
                .AddInMemoryIdentityResources(IdentityResources)
                .AddInMemoryApiScopes(ApiScopes);

            idsrv.AddBackChannelLogoutHttpClient();
        };

        OnConfigureApp += app =>
        {
            app.MapGet("/account/create", context =>
            {
                return Task.CompletedTask;
            });

            app.MapGet("/account/login", async ctx =>
            {
                var props = PropsToSignIn ?? new AuthenticationProperties();
                await ctx.SignInAsync(UserToSignIn, props);
            });

            app.MapGet("/account/logout", async ctx =>
            {
                // signout as if the user were prompted
                await ctx.SignOutAsync(PropsToSignIn);

                var logoutId = ctx.Request.Query["logoutId"];
                var interaction = ctx.RequestServices.GetRequiredService<IIdentityServerInteractionService>();

                var signOutContext = await interaction.GetLogoutContextAsync(logoutId);

                ctx.Response.Redirect(signOutContext.PostLogoutRedirectUri ?? "/");
            });

            app.MapGet("/__signin", async ctx =>
            {
                var props = PropsToSignIn ?? new AuthenticationProperties();
                await ctx.SignInAsync(UserToSignIn, props);

                ctx.Response.StatusCode = 204;
            });

            app.MapGet("/__signout", async ctx =>
            {
                var props = PropsToSignIn ?? new AuthenticationProperties();
                await ctx.SignOutAsync(props);
                ctx.Response.StatusCode = (int)HttpStatusCode.NoContent;
            });
        };

        BrowserClient = Internet.BuildHttpClient<IdentityServerClient>(Url());
        BrowserClient.Host = this;
    }

    protected override void ConfigureApp(IApplicationBuilder app)
    {
        app.UseIdentityServer();
        app.UseAuthorization();

        app.UseRouting();
        base.ConfigureApp(app);
    }

    public IdentityServerClient BrowserClient;

    public List<Client> Clients { get; set; } = new();
    public List<IdentityResource> IdentityResources { get; set; } = new()
    {
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email(),
    };

    public List<ApiScope> ApiScopes { get; set; } = [];

    public ClaimsPrincipal UserToSignIn { get; set; }
    public AuthenticationProperties? PropsToSignIn { get; set; }

    public Client AddClient(string clientId, Uri uri)
    {
        var client = new Client()
        {
            ClientId = clientId ?? throw new InvalidOperationException("missing client id"),
            ClientSecrets = { new Secret(The.ClientSecret.Sha256()) },
            AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
            RedirectUris = [new Uri(uri, "signin-oidc").ToString()],
            PostLogoutRedirectUris = [new Uri(uri, "signout-callback-oidc").ToString()],
            AllowOfflineAccess = true,
            AllowedScopes = { "openid", "profile", The.Scope, "offline_access" },
        };
        Clients.Add(client);
        return client;
    }

    public Client AddClientFor(BffFrontend frontend, IEnumerable<Uri> baseUris, string? callbackPath = null)
    {
        var options = new OpenIdConnectOptions();
        frontend.ConfigureOpenIdConnectOptions?.Invoke(options);

        var clientId = options.ClientId ?? frontend.Name;

        var existing = Clients.FirstOrDefault(c => c.ClientId == clientId);
        if (existing != null)
        {
            Clients.Remove(existing);
        }

        var clientSecret = options.ClientSecret ?? The.ClientSecret;
        callbackPath ??= options.CallbackPath;

        var redirectUris = baseUris
            .Select(baseUri => new Uri(baseUri, (frontend.MatchingCriteria.MatchingPath ?? string.Empty) + callbackPath).ToString())
            .ToList();

        var postLogoutRedirectUris = baseUris
            .Select(baseUri => new Uri(baseUri, options.SignedOutCallbackPath).ToString())
            .ToList();

        var client = new Client()
        {
            ClientId = clientId,
            ClientSecrets = { new Secret(clientSecret.Sha256()) },
            AllowedGrantTypes = GrantTypes.CodeAndClientCredentials,
            RedirectUris = redirectUris,
            PostLogoutRedirectUris = postLogoutRedirectUris,
            AllowOfflineAccess = true,
            AllowedScopes = options.Scope.Any()
                ? options.Scope
                : ["openid", "profile", The.Scope],
        };
        Clients.Add(client);

        return client;
    }

    public class IdentityServerClient(RedirectHandler handler, CookieContainer cookies) : HttpClient(handler), IHttpClient<IdentityServerClient>
    {
        public CookieContainer Cookies { get; } = cookies;

        public RedirectHandler RedirectHandler = handler;

        public static IdentityServerClient Build(RedirectHandler handler, CookieContainer cookies) => new(handler, cookies);
        public IdentityServerTestHost Host = null!;

        public async Task IssueSessionCookieAsync(string sub, string? sid = null)
        {
            Host.PropsToSignIn = new();

            if (!string.IsNullOrWhiteSpace(sid))
            {
                Host.PropsToSignIn.Items.Add("session_id", sid);
            }

            await IssueSessionCookieAsync(new Claim("sub", sub));

            Host.PropsToSignIn = null;

        }
        public async Task IssueSessionCookieAsync(params Claim[] claims)
        {
            var previousUser = Host.UserToSignIn;
            try
            {
                Host.UserToSignIn = new ClaimsPrincipal(new ClaimsIdentity(claims, "test", "name", "role"));
                var response = await GetAsync(Host.Url("__signin"));
                response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            }
            finally
            {
                Host.UserToSignIn = previousUser;
            }

        }

        public async Task RevokeIdentityServerSession()
        {
            var response = await GetAsync(Host.Url("__signout"));
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

            Host.PropsToSignIn = null;
        }
    }

}
