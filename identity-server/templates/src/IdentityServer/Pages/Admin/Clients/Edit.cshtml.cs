using IdentityServerTemplate.Pages.Admin.ApiScopes;
using IdentityServerTemplate.Pages.Admin.IdentityScopes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace IdentityServerTemplate.Pages.Admin.Clients;

[SecurityHeaders]
[Authorize(Config.Policies.Admin)]
public class EditModel(
    ClientRepository clientRepository,
    ApiScopeRepository apiScopeRepository,
    IdentityScopeRepository identityScopeRepository,
    IHttpContextAccessor httpContextAccessor
    ) : PageModel
{
    [BindProperty]
    public EditClientModel InputModel { get; set; } = default!;

    public bool Updated { get; set; }
    public List<ApiScopeSummaryModel> ApiScopes { get; set; } = [];
    public List<IdentityScopeSummaryModel> IdentityScopes { get; set; } = [];

    [BindProperty]
    public string? Button { get; set; }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        var model = await clientRepository.GetByIdAsync(id);
        if (model == null)
        {
            return RedirectToPage("/Admin/Clients/Index");
        }
        else
        {
            ApiScopes = [.. (await apiScopeRepository.GetAllAsync())];

            if (model.Flow == Flow.CodeFlowWithPkce)
            {
                IdentityScopes = [.. (await identityScopeRepository.GetAllAsync())];
            }

            InputModel = model;
            InputModel.ClientConfigurationSample = model.Flow == Flow.CodeFlowWithPkce ?
                GetWebClientConfigurationSnippet(model) :
                GetClientCredentialsConfigurationSnippet(model);

            return Page();
        }
    }

    public async Task<IActionResult> OnPostAsync(string id)
    {
        if (Button == "delete")
        {
            await clientRepository.DeleteAsync(id);
            return RedirectToPage("/Admin/Clients/Index");
        }

        if (ModelState.IsValid)
        {
            await clientRepository.UpdateAsync(InputModel);
            Updated = true;

            return RedirectToPage("/Admin/Clients/Edit", new { id });
        }

        ApiScopes = [.. (await apiScopeRepository.GetAllAsync())];

        if (InputModel.Flow == Flow.CodeFlowWithPkce)
        {
            IdentityScopes = [.. (await identityScopeRepository.GetAllAsync())];
        }

        return Page();
    }

    private string GetWebClientConfigurationSnippet(EditClientModel Input)
    {
        var authority = httpContextAccessor.HttpContext?.Request.Scheme + "://" +
                        httpContextAccessor.HttpContext?.Request.Host + "/";

        var clientId = Input.ClientId;
        var cookieName = "__Host-MyClientApp";
        var scheme = "oidc";

        // build the lines that add each scope
        var scopeLines = "";
        if (Input.AllowedScopes?.Any() == true)
        {
            scopeLines = string.Join(
                Environment.NewLine,
                Input.AllowedScopes
                     .Select(s => $"        options.Scope.Add(\"{s}\");")
            );
        }

        var secretLine = @"        // Uncomment the following line to use a configured client secret for confidential clients
        // options.ClientSecret = ""<client-secret>"";";

        var snippet = $@"builder.Services.AddAuthentication(options =>
    {{
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = ""{scheme}"";
    }})
    .AddCookie(options =>
    {{
        options.Cookie.Name = ""{cookieName}"";
    }})
    .AddOpenIdConnect(authenticationScheme: ""{scheme}"", options =>
    {{
        options.Authority = ""{authority}"";

        options.ClientId = ""{clientId}"";
{secretLine}

        // code flow + PKCE (PKCE is turned on by default)
        options.ResponseType = ""code"";
        options.UsePkce = true;

        options.Scope.Clear();
{scopeLines}

        // not mapped by default
        options.ClaimActions.MapAll();
        options.ClaimActions.MapCustomJson(""address"", json => json.GetRawText());

        options.GetClaimsFromUserInfoEndpoint = true;
        options.SaveTokens = true;

        options.TokenValidationParameters = new TokenValidationParameters
        {{
            NameClaimType = JwtClaimTypes.Name,
            RoleClaimType = JwtClaimTypes.Role,
        }};

        options.DisableTelemetry = true;
    }});";

        return snippet;
    }

    private string GetClientCredentialsConfigurationSnippet(EditClientModel Input)
    {
        var authority = httpContextAccessor.HttpContext?.Request.Scheme + "://" +
                        httpContextAccessor.HttpContext?.Request.Host + "/";

        var clientId = Input.ClientId;
        var clientSecret = "your-client-secret";

        // build the lines that add each scope
        var scopeLines = "";
        if (Input.AllowedScopes?.Any() == true)
        {
            scopeLines = string.Join(
                Environment.NewLine,
                Input.AllowedScopes
                     .Select(s => $"        options.Scope.Add(\"{s}\");")
            );
        }

        var snippet = $@"async Task<TokenResponse> RequestTokenAsync()
{{
    var client = new HttpClient();

    var disco = await client.GetDiscoveryDocumentAsync(""{authority}"");
    if (disco.IsError)
    {{
        throw new Exception(disco.Error);
    }}

    var response = await client.RequestClientCredentialsTokenAsync(new ClientCredentialsTokenRequest
    {{
        Address = disco.TokenEndpoint,
        ClientId = ""{clientId}"",
        ClientSecret = ""{clientSecret}"",

        // Removing the scope lines for client credentials flow will request all allowed scopes.
        // Specify scopes if you want to limit the request to only those scopes, even if more are allowed.

{scopeLines}
    }});

    if (response.IsError)
    {{
        throw new Exception(response.Error);
    }}

    return response;
}}";

        return snippet;
    }

}
