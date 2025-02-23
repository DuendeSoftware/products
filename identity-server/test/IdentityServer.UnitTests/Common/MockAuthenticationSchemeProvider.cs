// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;

namespace UnitTests.Common;

internal class MockAuthenticationSchemeProvider : IAuthenticationSchemeProvider
{
    public string Default { get; set; } = "scheme";
    public List<AuthenticationScheme> Schemes { get; set; } = new List<AuthenticationScheme>()
    {
        new AuthenticationScheme("scheme", null, typeof(MockAuthenticationHandler))
    };

    public void AddScheme(AuthenticationScheme scheme)
    {
        Schemes.Add(scheme);
    }

    public Task<IEnumerable<AuthenticationScheme>> GetAllSchemesAsync()
    {
        return Task.FromResult(Schemes.AsEnumerable());
    }

    public Task<AuthenticationScheme> GetDefaultAuthenticateSchemeAsync()
    {
        var scheme = Schemes.FirstOrDefault(x => x.Name == Default);
        return Task.FromResult(scheme);
    }

    public Task<AuthenticationScheme> GetDefaultChallengeSchemeAsync()
    {
        return GetDefaultAuthenticateSchemeAsync();
    }

    public Task<AuthenticationScheme> GetDefaultForbidSchemeAsync()
    {
        return GetDefaultAuthenticateSchemeAsync();
    }

    public Task<AuthenticationScheme> GetDefaultSignInSchemeAsync()
    {
        return GetDefaultAuthenticateSchemeAsync();
    }

    public Task<AuthenticationScheme> GetDefaultSignOutSchemeAsync()
    {
        return GetDefaultAuthenticateSchemeAsync();
    }

    public Task<IEnumerable<AuthenticationScheme>> GetRequestHandlerSchemesAsync()
    {
        return Task.FromResult(Schemes.AsEnumerable());
    }

    public Task<AuthenticationScheme> GetSchemeAsync(string name)
    {
        return Task.FromResult(Schemes.FirstOrDefault(x => x.Name == name));
    }

    public void RemoveScheme(string name)
    {
        var scheme = Schemes.FirstOrDefault(x => x.Name == name);
        if (scheme != null)
        {
            Schemes.Remove(scheme);
        }
    }
}