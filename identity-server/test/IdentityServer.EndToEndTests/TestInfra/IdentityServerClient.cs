// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using AngleSharp;
using AngleSharp.Html.Dom;

namespace Duende.IdentityServer.EndToEndTests.TestInfra;

/// <summary>
///     Client for the BFF. All the methods that can be invoked are here.
/// </summary>
public class IdentityServerClient
{
    private readonly HttpClient _client;

    public IdentityServerClient(HttpClient client)
    {
        _client = client;

        // Add a header that will trigger pre-flight cors checks
        _client.DefaultRequestHeaders.Add("X-CSRF", "1");
    }

    public async Task TriggerLogin(string userName = "alice", string password = "alice", CancellationToken ct = default)
    {
        var triggerLoginResponse = await _client.GetAsync("/bff/login");

        triggerLoginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var loginPage = triggerLoginResponse.RequestMessage?.RequestUri ??
                        throw new InvalidOperationException("Can't find the login page.");
        loginPage.AbsolutePath.ShouldBe("/Account/Login");

        var html = await triggerLoginResponse.Content.ReadAsStringAsync();
        var form = await ExtractFormFieldsAsync(html);

        form.Fields["Input.Username"] = "alice";
        form.Fields["Input.Password"] = "alice";
        form.Fields["Input.Button"] = "login";

        var postLoginResponse =
            await _client.PostAsync(new Uri(loginPage, form.FormUrl), new FormUrlEncodedContent(form.Fields), ct);

        postLoginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        postLoginResponse.RequestMessage?.RequestUri?.Authority.ShouldBe(_client.BaseAddress?.Authority,
            await postLoginResponse.Content.ReadAsStringAsync(ct));
    }

    private async Task<Form> ExtractFormFieldsAsync(string htmlContent)
    {
        // Create a configuration for AngleSharp
        var config = AngleSharp.Configuration.Default.WithDefaultLoader();

        // Load the HTML content into an AngleSharp browsing context
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(htmlContent));

        // Find the first form on the page
        var form = document.QuerySelector("form");
        if (form == null)
        {
            throw new InvalidOperationException("No form found in the provided HTML content.");
        }

        // Extract all form fields and their values
        var formFields = new Dictionary<string, string>();
        foreach (var element in form.QuerySelectorAll("input"))
        {
            var name = element.GetAttribute("name") ?? throw new InvalidOperationException("input doesn't have a name");
            if (string.IsNullOrEmpty(name))
            {
                continue; // Skip elements without a name attribute
            }

            var value = element.GetAttribute("value") ?? string.Empty;

            if (element is IHtmlSelectElement selectElement)
            {
                // Handle <select> elements by extracting the selected option's value
                var selectedOption = selectElement.SelectedOptions.Length > 0
                    ? selectElement.SelectedOptions[0].GetAttribute("value")
                    : string.Empty;
                value = selectedOption ?? string.Empty;
            }

            // Add the field to the dictionary
            formFields[name] = value;
        }

        return new Form
        {
            FormUrl = form.GetAttribute("action") ??
                      throw new InvalidOperationException("Failed to find the 'action' on the form"),
            Fields = formFields
        };
    }

    public async Task TriggerLogout()
    {
        // To trigger a logout, we need the logout claim
        var userClaims = await GetUserClaims();

        var logoutLink = userClaims.FirstOrDefault(x => x.Type == "bff:logout_url")
                         ?? throw new InvalidOperationException("Failed to find logout link claim");

        var logoutResponse = await _client.GetAsync(logoutLink.Value.ToString());
        logoutResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public async Task<UserClaim[]> GetUserClaims()
    {
        var userClaimsString = await _client.GetStringAsync("/bff/user");
        var userClaims = JsonSerializer.Deserialize<UserClaim[]>(userClaimsString, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        })!;
        return userClaims;
    }

    public async Task InvokeApi(string url, HttpStatusCode expectedResponse = HttpStatusCode.OK)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.ShouldBe(expectedResponse);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            response.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        }
    }

    public record UserClaim
    {
        public required string Type { get; init; }
        public required object Value { get; init; }
    }

    private record Form
    {
        public required string FormUrl { get; init; }
        public required Dictionary<string, string> Fields { get; init; }
    }
}
