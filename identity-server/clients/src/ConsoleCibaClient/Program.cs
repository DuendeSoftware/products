// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Clients;
using Duende.IdentityModel;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

var builder = Host.CreateApplicationBuilder(args);

// Add ServiceDefaults from Aspire
builder.AddServiceDefaults();

// Register a named HttpClient with service discovery support.
// The AddServiceDiscovery extension enables Aspire to resolve the actual endpoint at runtime.
builder.Services.AddHttpClient("SimpleApi", client =>
{
    client.BaseAddress = new Uri("https://simple-api");
})
.AddServiceDiscovery();

// Resolve the authority from the configuration.
var authority = builder.Configuration["is-host"];

IDiscoveryCache _cache = new DiscoveryCache(authority);

// Build the host so we can resolve the HttpClientFactory.
var host = builder.Build();
var httpClientFactory = host.Services.GetRequiredService<IHttpClientFactory>();

IDiscoveryCache _cache = new DiscoveryCache(authority);

var loginResponse = await RequestBackchannelLoginAsync();

var tokenResponse = await RequestTokenAsync(loginResponse);
tokenResponse.Show();

await CallServiceAsync(tokenResponse.AccessToken);

// Graceful shutdown
Environment.Exit(0);

async Task<BackchannelAuthenticationResponse> RequestBackchannelLoginAsync()
{
    var disco = await _cache.GetAsync();
    if (disco.IsError) throw new Exception(disco.Error);

    var cibaEp = disco.BackchannelAuthenticationEndpoint;

    var username = "alice";
    var bindingMessage = Guid.NewGuid().ToString("N")[..10];

    var req = new BackchannelAuthenticationRequest()
    {
        Address = cibaEp,
        ClientId = "ciba",
        ClientSecret = "secret",
        Scope = "openid profile email resource1.scope1 offline_access",
        LoginHint = username,
        //IdTokenHint = "eyJhbGciOiJSUzI1NiIsImtpZCI6IkYyNjZCQzA3NTFBNjIyNDkzMzFDMzI4QUQ1RkIwMkJGIiwidHlwIjoiSldUIn0.eyJpc3MiOiJodHRwczovL2xvY2FsaG9zdDo1MDAxIiwibmJmIjoxNjM4NDc3MDE2LCJpYXQiOjE2Mzg0NzcwMTYsImV4cCI6MTYzODQ3NzMxNiwiYXVkIjoiY2liYSIsImFtciI6WyJwd2QiXSwiYXRfaGFzaCI6ImE1angwelVQZ2twczBVS1J5VjBUWmciLCJzaWQiOiIzQTJDQTJDNjdBNTAwQ0I2REY1QzEyRUZDMzlCQTI2MiIsInN1YiI6IjgxODcyNyIsImF1dGhfdGltZSI6MTYzODQ3NzAwOCwiaWRwIjoibG9jYWwifQ.GAIHXYgEtXw5NasR0zPMW3jSKBuWujzwwnXJnfHdulKX-I3r47N0iqHm5v5V0xfLYdrmntjLgmdm0DSvdXswtZ1dh96DqS1zVm6yQ2V0zsA2u8uOt1RG8qtjd5z4Gb_wTvks4rbUiwi008FOZfRuqbMJJDSscy_YdEJqyQahdzkcUnWZwdbY8L2RUTxlAAWQxktpIbaFnxfr8PFQpyTcyQyw0b7xmYd9ogR7JyOff7IJIHPDur0wbRdpI1FDE_VVCgoze8GVAbVxXPtj4CtWHAv07MJxa9SdA_N-lBcrZ3PHTKQ5t1gFXwdQvp3togUJl33mJSru3lqfK36pn8y8ow",
        BindingMessage = bindingMessage,
        RequestedExpiry = 200
    };

    var useRequestObject = false;
    if (useRequestObject)
    {
        req = new BackchannelAuthenticationRequest
        {
            Address = req.Address,
            ClientId = req.ClientId,
            ClientSecret = req.ClientSecret,
            RequestObject = CreateRequestObject(new Dictionary<string, string>
                {
                    { OidcConstants.BackchannelAuthenticationRequest.Scope, req.Scope },
                    { OidcConstants.BackchannelAuthenticationRequest.LoginHint, req.LoginHint },
                    { OidcConstants.BackchannelAuthenticationRequest.IdTokenHint, req.IdTokenHint },
                    { OidcConstants.BackchannelAuthenticationRequest.BindingMessage, req.BindingMessage },
                }),
        };
    }

    var client = new HttpClient();
    var response = await client.RequestBackchannelAuthenticationAsync(req);

    if (response.IsError) throw new Exception(response.Error);

    Console.WriteLine($"Login Hint                  : {username}");
    Console.WriteLine($"Binding Message             : {bindingMessage}");
    Console.WriteLine($"Authentication Request Id   : {response.AuthenticationRequestId}");
    Console.WriteLine($"Expires In                  : {response.ExpiresIn}");
    Console.WriteLine($"Interval                    : {response.Interval}");
    Console.WriteLine();

    return response;
}

async Task<TokenResponse> RequestTokenAsync(BackchannelAuthenticationResponse authorizeResponse)
{
    Console.WriteLine($"Polling the token endpoint.");

    var disco = await _cache.GetAsync();
    if (disco.IsError) throw new Exception(disco.Error);

    var client = new HttpClient();

    while (true)
    {
        var response = await client.RequestBackchannelAuthenticationTokenAsync(new BackchannelAuthenticationTokenRequest
        {
            Address = disco.TokenEndpoint,
            ClientId = "ciba",
            ClientSecret = "secret",
            AuthenticationRequestId = authorizeResponse.AuthenticationRequestId
        });

        if (response.IsError)
        {
            if (response.Error == OidcConstants.TokenErrors.AuthorizationPending || response.Error == OidcConstants.TokenErrors.SlowDown)
            {
                Console.WriteLine($"{response.Error}...waiting.");
                Thread.Sleep(authorizeResponse.Interval.Value * 1000);
            }
            else
            {
                throw new Exception(response.Error);
            }
        }
        else
        {
            return response;
        }
    }
}

async Task CallServiceAsync(string token)
{
    // Resolve the HttpClient from DI.
    var client = httpClientFactory.CreateClient("SimpleApi");

    client.SetBearerToken(token);
    var response = await client.GetStringAsync("identity");

    "\nService claims:".ConsoleGreen();
    Console.WriteLine(response.PrettyPrintJson());
}

string CreateRequestObject(IDictionary<string, string> values)
{
    var claims = new List<Claim>()
        {
            new(JwtClaimTypes.JwtId, Guid.NewGuid().ToString("n"))
        };

    foreach (var item in values)
    {
        if (!string.IsNullOrWhiteSpace(item.Value))
        {
            claims.Add(new Claim(item.Key, item.Value));
        }
    }

    const string rsaKey =
        """
            {
                "d":"GmiaucNIzdvsEzGjZjd43SDToy1pz-Ph-shsOUXXh-dsYNGftITGerp8bO1iryXh_zUEo8oDK3r1y4klTonQ6bLsWw4ogjLPmL3yiqsoSjJa1G2Ymh_RY_sFZLLXAcrmpbzdWIAkgkHSZTaliL6g57vA7gxvd8L4s82wgGer_JmURI0ECbaCg98JVS0Srtf9GeTRHoX4foLWKc1Vq6NHthzqRMLZe-aRBNU9IMvXNd7kCcIbHCM3GTD_8cFj135nBPP2HOgC_ZXI1txsEf-djqJj8W5vaM7ViKU28IDv1gZGH3CatoysYx6jv1XJVvb2PH8RbFKbJmeyUm3Wvo-rgQ",
                "dp":"YNjVBTCIwZD65WCht5ve06vnBLP_Po1NtL_4lkholmPzJ5jbLYBU8f5foNp8DVJBdFQW7wcLmx85-NC5Pl1ZeyA-Ecbw4fDraa5Z4wUKlF0LT6VV79rfOF19y8kwf6MigyrDqMLcH_CRnRGg5NfDsijlZXffINGuxg6wWzhiqqE",
                "dq":"LfMDQbvTFNngkZjKkN2CBh5_MBG6Yrmfy4kWA8IC2HQqID5FtreiY2MTAwoDcoINfh3S5CItpuq94tlB2t-VUv8wunhbngHiB5xUprwGAAnwJ3DL39D2m43i_3YP-UO1TgZQUAOh7Jrd4foatpatTvBtY3F1DrCrUKE5Kkn770M",
                "e":"AQAB",
                "kid":"ZzAjSnraU3bkWGnnAqLapYGpTyNfLbjbzgAPbbW2GEA",
                "kty":"RSA",
                "n":"wWwQFtSzeRjjerpEM5Rmqz_DsNaZ9S1Bw6UbZkDLowuuTCjBWUax0vBMMxdy6XjEEK4Oq9lKMvx9JzjmeJf1knoqSNrox3Ka0rnxXpNAz6sATvme8p9mTXyp0cX4lF4U2J54xa2_S9NF5QWvpXvBeC4GAJx7QaSw4zrUkrc6XyaAiFnLhQEwKJCwUw4NOqIuYvYp_IXhw-5Ti_icDlZS-282PcccnBeOcX7vc21pozibIdmZJKqXNsL1Ibx5Nkx1F1jLnekJAmdaACDjYRLL_6n3W4wUp19UvzB1lGtXcJKLLkqB6YDiZNu16OSiSprfmrRXvYmvD8m6Fnl5aetgKw",
                "p":"7enorp9Pm9XSHaCvQyENcvdU99WCPbnp8vc0KnY_0g9UdX4ZDH07JwKu6DQEwfmUA1qspC-e_KFWTl3x0-I2eJRnHjLOoLrTjrVSBRhBMGEH5PvtZTTThnIY2LReH-6EhceGvcsJ_MhNDUEZLykiH1OnKhmRuvSdhi8oiETqtPE",
                "q":"0CBLGi_kRPLqI8yfVkpBbA9zkCAshgrWWn9hsq6a7Zl2LcLaLBRUxH0q1jWnXgeJh9o5v8sYGXwhbrmuypw7kJ0uA3OgEzSsNvX5Ay3R9sNel-3Mqm8Me5OfWWvmTEBOci8RwHstdR-7b9ZT13jk-dsZI7OlV_uBja1ny9Nz9ts",
                "qi":"pG6J4dcUDrDndMxa-ee1yG4KjZqqyCQcmPAfqklI2LmnpRIjcK78scclvpboI3JQyg6RCEKVMwAhVtQM6cBcIO3JrHgqeYDblp5wXHjto70HVW6Z8kBruNx1AH9E8LzNvSRL-JVTFzBkJuNgzKQfD0G77tQRgJ-Ri7qu3_9o1M4"
            }
            """;

    var now = DateTime.UtcNow;

    var token = new JwtSecurityToken(
        "ciba",
        authority,
        claims,
        now,
        now.AddMinutes(1),
        new SigningCredentials(new JsonWebKey(rsaKey), "RS256")
    );

    var tokenHandler = new JwtSecurityTokenHandler();
    tokenHandler.OutboundClaimTypeMap.Clear();

    return tokenHandler.WriteToken(token);
}
