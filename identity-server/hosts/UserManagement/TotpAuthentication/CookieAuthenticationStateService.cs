// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace TotpAuthentication;

public sealed class CookieAuthenticationStateService(
    IDataProtectionProvider dataProtectionProvider,
    IHttpContextAccessor httpContextAccessor,
    IWebHostEnvironment environment)
{
    private const string CookieName = "Auth";
    private const string Purpose = "AuthenticationState.v1";
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromMinutes(5);

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(Purpose);

    private HttpContext HttpContext => httpContextAccessor.HttpContext
                                       ?? throw new InvalidOperationException("HttpContext is not available.");

    public void StoreAuthenticationState(AuthenticationStateData authenticationStateData)
    {
        var json = JsonSerializer.Serialize(authenticationStateData);
        var protectedData = _protector.Protect(json);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.Add(CookieLifetime)
        };

        HttpContext.Response.Cookies.Append(CookieName, protectedData, cookieOptions);
    }

    public bool TryRetrieveAuthenticationState([NotNullWhen(true)] out AuthenticationStateData? authenticationState)
    {
        authenticationState = null;

        if (!HttpContext.Request.Cookies.TryGetValue(CookieName, out var encryptedData) ||
            string.IsNullOrEmpty(encryptedData))
        {
            return false;
        }

        try
        {
            var json = _protector.Unprotect(encryptedData);
            authenticationState = JsonSerializer.Deserialize<AuthenticationStateData>(json);
            return authenticationState is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public AuthenticationStateData? GetAuthenticationState()
    {
        if (TryRetrieveAuthenticationState(out var authenticationState))
        {
            return authenticationState;
        }

        return null;
    }

    public void ClearAuthenticationState() => HttpContext.Response.Cookies.Delete(CookieName, new CookieOptions
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        IsEssential = true
    });
}
