// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.AspNetCore.Authentication;

namespace UnitTests.Common;

internal class MockAuthenticationSchemeProvider : IAuthenticationSchemeProvider
{
    public string Default { get; set; } = "scheme";
    public List<AuthenticationScheme> Schemes { get; set; } = new List<AuthenticationScheme>()
    {
        new AuthenticationScheme("scheme", null, typeof(MockAuthenticationHandler))
    };

    public void AddScheme(AuthenticationScheme scheme) => Schemes.Add(scheme);

    public Task<IEnumerable<AuthenticationScheme>> GetAllSchemesAsync() => Task.FromResult(Schemes.AsEnumerable());

    public Task<AuthenticationScheme> GetDefaultAuthenticateSchemeAsync()
    {
        var scheme = Schemes.FirstOrDefault(x => x.Name == Default);
        return Task.FromResult(scheme);
    }

    public Task<AuthenticationScheme> GetDefaultChallengeSchemeAsync() => GetDefaultAuthenticateSchemeAsync();

    public Task<AuthenticationScheme> GetDefaultForbidSchemeAsync() => GetDefaultAuthenticateSchemeAsync();

    public Task<AuthenticationScheme> GetDefaultSignInSchemeAsync() => GetDefaultAuthenticateSchemeAsync();

    public Task<AuthenticationScheme> GetDefaultSignOutSchemeAsync() => GetDefaultAuthenticateSchemeAsync();

    public Task<IEnumerable<AuthenticationScheme>> GetRequestHandlerSchemesAsync() => Task.FromResult(Schemes.AsEnumerable());

    public Task<AuthenticationScheme> GetSchemeAsync(string name) => Task.FromResult(Schemes.FirstOrDefault(x => x.Name == name));

    public void RemoveScheme(string name)
    {
        var scheme = Schemes.FirstOrDefault(x => x.Name == name);
        if (scheme != null)
        {
            Schemes.Remove(scheme);
        }
    }
}
