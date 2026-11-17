// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace AuthenticationFlowWithWebComponent.ServerComponent;

public static class WebComponentConfigurationBuilder
{
    public static object Build(WebComponentOptions options)
    {
        var primaryAuthenticationMethods = BuildPrimaryAuthenticationMethods(options);

        var config = new
        {
            //OTP (sms, email), PassKeys, etc
            //Primary must be identifying
            primaryAuthenticators = primaryAuthenticationMethods.ToArray(),
            //TOTP, SMS
            //Secondary could be identifying but doesn't need to be
            secondaryAuthenticators = new[]
            {
                new
                {
                    type = "totp",
                    title = "Authenticator App",
                    description = "Multi-factor authentication using TOTP",
                    endpoints = new
                    {
                        verifyMfa = "/api/auth/mfa/verify"
                    }
                }
            },
            settings = new
            {
                rememberMeEnabled = options.Session.EnableRememberMe
            }
        };
        return config;
    }

    private static List<object> BuildPrimaryAuthenticationMethods(WebComponentOptions options)
    {
        var methods = new List<object>();

        if (options.Authentication.IsOtpEnabled)
        {
            methods.Add(BuildOtp(options));
        }

        if (options.Authentication.IsExternalAuthEnabled)
        {
            methods.Add(BuildExternal(options));
        }

        return methods;
    }

    private static object BuildOtp(WebComponentOptions options) => new
    {
        type = "otp",
        enabled = true,
        priority = 1,
        title = "Email with Code",
        description = "Receive a one-time code via email",
        config = new
        {
            otpLength = options.Otp.Length,
            otpFormat = options.Otp.Format,
            otpExpirySeconds = options.Otp.ExpirySeconds,
            allowResend = options.Otp.AllowResend,
            resendCooldownSeconds = options.Otp.ResendCooldownSeconds
        },
        endpoints = new
        {
            sendOtp = "/api/auth/otp/send",
            verifyOtp = "/api/auth/otp/verify"
        }
    };

    private static object BuildExternal(WebComponentOptions options) => new
    {
        type = "external",
        enabled = true,
        priority = 2,
        title = "External Provider",
        description = "Sign in with Google or other providers",
        config = new
        {
            providers = options.Authentication.ExternalProviders.Select(p => new
            {
                id = p.Id,
                displayName = p.DisplayName,
                challengeUrl = p.ChallengeUrl
            }).ToArray()
        }
    };
}
