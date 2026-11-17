// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace AuthenticationFlowWithWebComponent.ServerComponent;

/// <summary>
/// Service for managing temporary authentication state for MFA challenges in cookies
/// </summary>
public sealed class CookieAuthenticationStateService
{
    private const string CookieName = "__Host-Auth";
    private const string Purpose = "AuthenticationState.v1";
    private static readonly TimeSpan CookieLifetime = TimeSpan.FromMinutes(5);

    private readonly IDataProtector _protector;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CookieAuthenticationStateService"/> class
    /// </summary>
    public CookieAuthenticationStateService(
        IDataProtectionProvider dataProtectionProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext HttpContext => _httpContextAccessor.HttpContext
                                       ?? throw new InvalidOperationException("HttpContext is not available.");

    /// <summary>
    /// Stores authentication state in an encrypted cookie
    /// </summary>
    public void StoreAuthenticationState(AuthenticationStateData authenticationStateData)
    {
        var json = JsonSerializer.Serialize(authenticationStateData);
        var protectedData = _protector.Protect(json);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.Add(CookieLifetime)
        };

        HttpContext.Response.Cookies.Append(CookieName, protectedData, cookieOptions);
    }

    /// <summary>
    /// Retrieves authentication state from the encrypted cookie
    /// </summary>
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

    /// <summary>
    /// Clears the authentication state cookie
    /// </summary>
    public void ClearAuthenticationState() => HttpContext.Response.Cookies.Delete(CookieName, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        IsEssential = true
    });
}

/// <summary>
/// Represents temporary authentication state for multi-step authentication flows
/// </summary>
/// <param name="UserId">The user's subject identifier</param>
/// <param name="Email">The user's email address</param>
/// <param name="RememberMe">Whether the user selected "remember me"</param>
/// <param name="ReturnUrl">Optional return URL after authentication</param>
public record AuthenticationStateData(string UserId, string Email, bool RememberMe, string? ReturnUrl);
