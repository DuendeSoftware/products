// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace OtpIdentityServer;

public sealed class OtpCookieService(
    IDataProtectionProvider dataProtectionProvider,
    IHttpContextAccessor httpContextAccessor)
{
    private const string CookieName = "__Host-OtpAuth";
    private const string Purpose = "OtpAuthentication.v1";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(Purpose);

    private HttpContext HttpContext => httpContextAccessor.HttpContext
                                       ?? throw new InvalidOperationException("HttpContext is not available.");

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
            Expires = otpData.ExpiresAtUtc
        };

        HttpContext.Response.Cookies.Append(CookieName, protectedData, cookieOptions);
    }

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
            // If decryption or deserialization fails (tampered cookie, key rotation, etc.), return false
            return false;
        }
    }

    public void ClearOtpData() => HttpContext.Response.Cookies.Delete(CookieName, new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        IsEssential = true
    });
}
