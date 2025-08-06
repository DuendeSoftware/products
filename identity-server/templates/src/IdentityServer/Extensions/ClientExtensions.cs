using Duende.IdentityServer.Models;

namespace IdentityServerTemplate.Extensions;

public static class ClientExtensions
{
    public static void InferInteractiveClientURLs(
        this Client client,
        string baseUrl,
        string redirectPath,
        string initiateLoginPath,
        string postLogoutRedirectPath,
        string frontChannelLogoutPath)
    {
        // Only applies to interactive (code-flow) clients
        if (!client.AllowedGrantTypes.Contains(GrantType.AuthorizationCode))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return;
        }

        // Normalise the supplied base URL
        var baseUri = new Uri(baseUrl.TrimEnd('/'));
        var authority = baseUri.GetLeftPart(UriPartial.Authority);

        var redirectUri = new Uri(new Uri(authority + "/"),
                                  redirectPath.TrimStart('/'))
                          .ToString();

        client.RedirectUris.Clear();
        client.RedirectUris.Add(redirectUri);

        client.PostLogoutRedirectUris.Clear();
        client.PostLogoutRedirectUris.Add($"{authority}/{postLogoutRedirectPath.TrimStart('/')}");

        client.InitiateLoginUri = $"{authority}/{initiateLoginPath.TrimStart('/')}";
        client.FrontChannelLogoutUri = $"{authority}/{frontChannelLogoutPath.TrimStart('/')}";
    }
}
