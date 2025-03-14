using BffBlazor;
using BffBlazor.Components;
using Duende.Bff.Blazor;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// BFF setup for blazor
builder.Services.AddBff()
    .AddServerSideSessions() // Add in-memory implementation of server side sessions
    .AddBlazorServer();

// Register an abstraction for retrieving weather forecasts that can run on the server. 
// On the client, in WASM, this will be retrieved via an HTTP call to the server.
builder.Services.AddSingleton<IWeatherClient, ServerWeatherClient>();

// Configure the authentication
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "cookie";
        options.DefaultChallengeScheme = "oidc";
        options.DefaultSignOutScheme = "oidc";
    })
    .AddCookie("cookie", options =>
    {
        options.Cookie.Name = "__Host-blazor";

        // Because we use an identity server that's configured on a different site
        // (duendesoftware.com vs localhost), we need to configure the SameSite property to Lax. 
        // Setting it to Strict would cause the authentication cookie not to be sent after loggin in.
        // The user would have to refresh the page to get the cookie.
        // Recommendation: Set it to 'strict' if your IDP is on the same site as your BFF.
        options.Cookie.SameSite = SameSiteMode.Lax;
    })
    .AddOpenIdConnect("oidc", options =>
    {
        options.Authority = "https://demo.duendesoftware.com";
        options.ClientId = "interactive.confidential";
        options.ClientSecret = "secret";
        options.ResponseType = "code";
        options.ResponseMode = "query";

        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;
        options.MapInboundClaims = false;

        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("api");
        options.Scope.Add("offline_access");

        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = "role";
    });

// Make sure authentication state is available to all components. 
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();

// Add the BFF middleware which performs anti forgery protection
app.UseBff();
app.UseAuthorization();
app.UseAntiforgery();

// Add the BFF management endpoints, such as login, logout, etc.
app.MapBffManagementEndpoints();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(BffBlazor.Client._Imports).Assembly);

// Example of local api endpoints. 
app.MapWeatherEndpoints();

app.Run();
