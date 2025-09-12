// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Identity;

namespace Duende.IdentityServer.AspNetIdentity;

#pragma warning disable CA1812 // This class is not instantiated directly, but rather used by the DI container
internal sealed class UserClaimsFactory<TUser> : IUserClaimsPrincipalFactory<TUser>
    where TUser : class
#pragma warning restore CA1812
{
    private readonly Decorator<IUserClaimsPrincipalFactory<TUser>> _inner;
    private readonly UserManager<TUser> _userManager;

    public UserClaimsFactory(Decorator<IUserClaimsPrincipalFactory<TUser>> inner, UserManager<TUser> userManager)
    {
        _inner = inner;
        _userManager = userManager;
    }

    public async Task<ClaimsPrincipal> CreateAsync(TUser user)
    {
        var principal = await _inner.Instance.CreateAsync(user);
        var identity = principal.Identities.First();

        if (!identity.HasClaim(x => x.Type == JwtClaimTypes.Subject))
        {
            var sub = await _userManager.GetUserIdAsync(user);
            identity.AddClaim(new Claim(JwtClaimTypes.Subject, sub));
        }

        var username = await _userManager.GetUserNameAsync(user);
        var usernameClaim = identity.FindFirst(claim =>
            claim.Type == _userManager.Options.ClaimsIdentity.UserNameClaimType && claim.Value == username);
        if (usernameClaim != null)
        {
            identity.RemoveClaim(usernameClaim);
            identity.AddClaim(new Claim(JwtClaimTypes.PreferredUserName, username));
        }

        if (!identity.HasClaim(x => x.Type == JwtClaimTypes.Name))
        {
            identity.AddClaim(new Claim(JwtClaimTypes.Name, username));
        }

        if (_userManager.SupportsUserEmail)
        {
            var email = await _userManager.GetEmailAsync(user);
            if (!string.IsNullOrWhiteSpace(email))
            {
                identity.AddClaims(new[]
                {
                    new Claim(JwtClaimTypes.EmailVerified,
                        await _userManager.IsEmailConfirmedAsync(user) ? "true" : "false", ClaimValueTypes.Boolean)
                });
            }
        }

        if (_userManager.SupportsUserPhoneNumber)
        {
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                identity.AddClaims(new[]
                {
                    new Claim(JwtClaimTypes.PhoneNumber, phoneNumber),
                    new Claim(JwtClaimTypes.PhoneNumberVerified,
                        await _userManager.IsPhoneNumberConfirmedAsync(user) ? "true" : "false",
                        ClaimValueTypes.Boolean)
                });
            }
        }

        return principal;
    }
}
