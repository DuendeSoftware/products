// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Duende.IdentityModel;

namespace Duende.IdentityServer.Test;

/// <summary>
/// Store for test users
/// </summary>
public class TestUserStore
{
    private readonly List<TestUser> _users;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestUserStore"/> class.
    /// </summary>
    /// <param name="users">The users.</param>
    public TestUserStore(List<TestUser> users)
    {
        _users = users;
    }

    /// <summary>
    /// Validates the credentials.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="password">The password.</param>
    /// <returns></returns>
    public bool ValidateCredentials(string username, string password)
    {
        var user = FindByUsername(username);

        if (user != null)
        {
            if (string.IsNullOrWhiteSpace(user.Password) && string.IsNullOrWhiteSpace(password))
            {
                return true;
            }

            return Equals(user.Password, password);
        }

        return false;
    }

    /// <summary>
    /// Finds the user by subject identifier.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <returns></returns>
    public TestUser FindBySubjectId(string subjectId)
    {
        return _users.FirstOrDefault(x => x.SubjectId == subjectId);
    }

    /// <summary>
    /// Finds the user by username.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <returns></returns>
    public TestUser FindByUsername(string username)
    {
        return _users.FirstOrDefault(x => x.Username.Equals(username, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds the user by external provider.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="userId">The user identifier.</param>
    /// <returns></returns>
    public TestUser FindByExternalProvider(string provider, string userId)
    {
        return _users.FirstOrDefault(x =>
            x.ProviderName == provider &&
            x.ProviderSubjectId == userId);
    }

    /// <summary>
    /// Automatically provisions a user.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="claims">The claims.</param>
    /// <returns></returns>
    public TestUser AutoProvisionUser(string provider, string userId, List<Claim> claims)
    {
        // create a list of claims that we want to transfer into our store
        var filtered = new List<Claim>();

        foreach (var claim in claims)
        {
            // if the external system sends a display name - translate that to the standard OIDC name claim
            if (claim.Type == ClaimTypes.Name)
            {
                filtered.Add(new Claim(JwtClaimTypes.Name, claim.Value));
            }
            // if the JWT handler has an outbound mapping to an OIDC claim use that
            else if (JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap.TryGetValue(claim.Type, out var value))
            {
                filtered.Add(new Claim(value, claim.Value));
            }
            // copy the claim as-is
            else
            {
                filtered.Add(claim);
            }
        }

        // if no display name was provided, try to construct by first and/or last name
        if (!filtered.Any(x => x.Type == JwtClaimTypes.Name))
        {
            var first = filtered.FirstOrDefault(x => x.Type == JwtClaimTypes.GivenName)?.Value;
            var last = filtered.FirstOrDefault(x => x.Type == JwtClaimTypes.FamilyName)?.Value;
            if (first != null && last != null)
            {
                filtered.Add(new Claim(JwtClaimTypes.Name, first + ' ' + last));
            }
            else if (first != null)
            {
                filtered.Add(new Claim(JwtClaimTypes.Name, first));
            }
            else if (last != null)
            {
                filtered.Add(new Claim(JwtClaimTypes.Name, last));
            }
        }

        // create a new unique subject id
        var sub = CryptoRandom.CreateUniqueId(format: CryptoRandom.OutputFormat.Hex);

        // check if a display name is available, otherwise fallback to subject id
        var name = filtered.FirstOrDefault(c => c.Type == JwtClaimTypes.Name)?.Value ?? sub;

        // create new user
        var user = new TestUser
        {
            SubjectId = sub,
            Username = name,
            ProviderName = provider,
            ProviderSubjectId = userId,
            Claims = filtered
        };

        // add user to in-memory store
        _users.Add(user);

        return user;
    }

    /// <summary>
    /// Adds a new a user.
    /// </summary>
    /// <returns></returns>
    public TestUser CreateUser(string username, string password, string name = null, string email = null)
    {
        if (_users.Any(x => x.Username == username))
        {
            throw new Exception("That username already exists.");
        }

        // create a new unique subject id
        var sub = CryptoRandom.CreateUniqueId(format: CryptoRandom.OutputFormat.Hex);

        var claims = new List<Claim>();
        if (!string.IsNullOrEmpty(name))
        {
            claims.Add(new Claim(ClaimTypes.Name, name));
        }
        if (!string.IsNullOrEmpty(email))
        {
            claims.Add(new Claim(ClaimTypes.Email, email));
        }

        // create new user
        var user = new TestUser
        {
            SubjectId = sub,
            Username = username,
            Password = password,
            Claims = claims
        };

        // add user to in-memory store
        _users.Add(user);

        // success
        return user;
    }
}
