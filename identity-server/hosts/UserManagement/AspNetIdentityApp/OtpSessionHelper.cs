// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace AspNetIdentityApp;

public sealed class OtpSessionHelper(IDataProtectionProvider dataProtectionProvider, IHttpContextAccessor httpContextAccessor)
{
    private const string CookieName = "__Host-IdentityOtp";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("AspNetIdentityApp.Otp.v1");

    private HttpContext HttpContext =>
        httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No active HttpContext.");

    internal void Write(string userId, string otp, DateTimeOffset expiresAt)
    {
        var json = JsonSerializer.Serialize(new OtpData(userId, otp, expiresAt.ToUnixTimeSeconds()));
        var protectedJson = _protector.Protect(json);

        HttpContext.Response.Cookies.Append(CookieName, protectedJson, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
            Expires = expiresAt
        });
    }

    internal bool TryRead([NotNullWhen(true)] out string? userId, [NotNullWhen(true)] out string? otp)
    {
        userId = null;
        otp = null;

        if (!HttpContext.Request.Cookies.TryGetValue(CookieName, out var protectedJson) ||
            string.IsNullOrWhiteSpace(protectedJson))
        {
            return false;
        }

        string json;
        try
        {
            json = _protector.Unprotect(protectedJson);
        }
        catch (CryptographicException)
        {
            return false;
        }

        OtpData? data;
        try
        {
            data = JsonSerializer.Deserialize<OtpData>(json);
        }
        catch (JsonException)
        {
            return false;
        }

        if (data is null || DateTimeOffset.FromUnixTimeSeconds(data.ExpiresAt) < DateTimeOffset.UtcNow)
        {
            return false;
        }

        userId = data.UserId;
        otp = data.Otp;
        return true;
    }

    internal void Clear() =>
        HttpContext.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            IsEssential = true
        });

    private sealed record OtpData(string UserId, string Otp, long ExpiresAt);
}
