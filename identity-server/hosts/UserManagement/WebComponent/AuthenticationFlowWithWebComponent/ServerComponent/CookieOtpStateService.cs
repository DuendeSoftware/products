// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace AuthenticationFlowWithWebComponent.ServerComponent;

/// <summary>
/// Service for managing temporary OTP authentication state in cookies
/// </summary>
public sealed class CookieOtpStateService
{
    private const string CookieName = "__Host-OtpAuth";
    private const string Purpose = "OtpAuthentication.v1";

    private readonly IDataProtector _protector;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeSpan _cookieLifetime;

    /// <summary>
    /// Initializes a new instance of the <see cref="CookieOtpStateService"/> class
    /// </summary>
    public CookieOtpStateService(
        IDataProtectionProvider dataProtectionProvider,
        IHttpContextAccessor httpContextAccessor,
        WebComponentOptions options)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _httpContextAccessor = httpContextAccessor;
        _cookieLifetime = TimeSpan.FromSeconds(options.Otp.ExpirySeconds);
    }

    private HttpContext HttpContext => _httpContextAccessor.HttpContext
                                       ?? throw new InvalidOperationException("HttpContext is not available.");

    /// <summary>
    /// Stores OTP data in an encrypted cookie
    /// </summary>
    public void StoreOtpData(OtpData otpData)
    {
        var json = JsonSerializer.Serialize(otpData);
        var protectedData = _protector.Protect(json);

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.Add(_cookieLifetime)
        };

        HttpContext.Response.Cookies.Append(CookieName, protectedData, cookieOptions);
    }

    /// <summary>
    /// Retrieves OTP data from the encrypted cookie
    /// </summary>
    public bool TryRetrieveOtpData([NotNullWhen(true)] out OtpData? otpData)
    {
        otpData = null;

        if (!HttpContext.Request.Cookies.TryGetValue(CookieName, out var encryptedData) ||
            string.IsNullOrEmpty(encryptedData))
        {
            return false;
        }

        try
        {
            var json = _protector.Unprotect(encryptedData);
            otpData = JsonSerializer.Deserialize<OtpData>(json);
            return otpData is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Clears the OTP data cookie
    /// </summary>
    public void ClearOtpData() => HttpContext.Response.Cookies.Delete(CookieName, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        IsEssential = true
    });
}

public record OtpData(string Email, string Token, string? ReturnUrl);
