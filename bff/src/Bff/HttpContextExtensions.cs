// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Net;
using Duende.AccessTokenManagement.OpenIdConnect;
using Duende.Bff.AccessTokenManagement;
using Duende.Bff.Configuration;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Http;
using AccessToken = Duende.Bff.AccessTokenManagement.AccessToken;


namespace Duende.Bff;

internal static class HttpContextExtensions
{
    public static void ReturnHttpProblem(this HttpContext context, string title, params (string key, string[] values)[] errors)
    {
        var problem = new HttpValidationProblemDetails()
        {
            Status = (int)HttpStatusCode.BadRequest,
            Title = title,
            Errors = errors.ToDictionary()
        };
        context.Response.StatusCode = problem.Status.Value;
        context.Response.ContentType = "application/problem+json";
        context.Response.WriteAsJsonAsync(problem);

    }

    public static void CheckForBffMiddleware(this HttpContext context, BffOptions options)
    {
        if (options.EnforceBffMiddleware)
        {
            var found = context.Items.TryGetValue(Constants.Middleware.AntiForgeryMarker, out _);
            if (!found)
            {
                throw new InvalidOperationException(
                    "The BFF middleware is missing in the pipeline. Add 'app.UseBff' after 'app.UseRouting' but before 'app.UseAuthorization'");
            }
        }
    }

    public static bool CheckAntiForgeryHeader(this HttpContext context, BffOptions options)
    {
        var antiForgeryHeader = context.Request.Headers[options.AntiForgeryHeaderName].FirstOrDefault();
        return antiForgeryHeader != null && antiForgeryHeader == options.AntiForgeryHeaderValue;
    }

    public static async Task<AccessTokenResult> GetManagedAccessToken(
        this HttpContext context,
        RequiredTokenType requiredTokenType,
        BffUserAccessTokenParameters? userAccessTokenParameters = null,
        CT ct = default)
    {
        if (requiredTokenType == RequiredTokenType.None)
        {
            return new NoAccessTokenResult();
        }

        var userAccessTokenRequestParameters = userAccessTokenParameters?.ToUserAccessTokenRequestParameters();

        RequiredTokenType[] shouldGetUserToken = [
            RequiredTokenType.User,
            RequiredTokenType.UserOrNone,
            RequiredTokenType.UserOrClient
        ];

        if (shouldGetUserToken.Contains(requiredTokenType))
        {
            // Only attempt to geta user token if user is authenticated
            if (context.User.Identity!.IsAuthenticated)
            {
                var userTokenResult = await
                    context.GetUserAccessTokenAsync(userAccessTokenRequestParameters, ct);

                if (userTokenResult.WasSuccessful(out var userToken, out var userTokenFailure))
                {
                    // Doing a case insensitive comparison here, because some openid connect providers return
                    // non standard casing: https://github.com/orgs/DuendeSoftware/discussions/280#discussioncomment-13862452
                    if (!string.Equals(userToken.AccessTokenType, OidcConstants.TokenResponse.DPoPTokenType,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return new BearerTokenResult
                        {
                            // Should we append the type here?
                            AccessToken = AccessToken.Parse(userToken.AccessToken.ToString())
                        };
                    }

                    if (userToken.DPoPJsonWebKey == null)
                    {
                        throw new InvalidOperationException("DPoP JsonWebKey was null");
                    }

                    return new DPoPTokenResult()
                    {
                        AccessToken = AccessToken.Parse(userToken.AccessToken.ToString()),
                        DPoPJsonWebKey = DPoPProofKey.Parse(userToken.DPoPJsonWebKey!.ToString()!)
                    };
                }

                if (requiredTokenType == RequiredTokenType.User)
                {
                    return new AccessTokenRetrievalError
                    {
                        Error = userTokenFailure.Error,
                        ErrorDescription = userTokenFailure.ErrorDescription
                    };
                }
                if (requiredTokenType == RequiredTokenType.UserOrNone)
                {
                    return new NoAccessTokenResult();
                }
            }
            else
            {
                // User is not authenticated
                // Handle unauthenticated scenarios based on token type
                // For UserOrClient, fall through to client token
                if (requiredTokenType == RequiredTokenType.User)
                {
                    return new AccessTokenRetrievalError
                    {
                        Error = "not_authenticated",
                        ErrorDescription = "User is not authenticated"
                    };
                }
                if (requiredTokenType == RequiredTokenType.UserOrNone)
                {
                    return new NoAccessTokenResult();
                }
            }
        }

        var clientTokenResult = await context.GetClientAccessTokenAsync(userAccessTokenRequestParameters, ct);
        if (clientTokenResult.WasSuccessful(out var clientToken, out var clientTokenFailure))
        {
            // Doing a case insensitive comparison here, because some openid connect providers return
            // non standard casing: https://github.com/orgs/DuendeSoftware/discussions/280#discussioncomment-13862452
            if (!string.Equals(clientToken.AccessTokenType, OidcConstants.TokenResponse.DPoPTokenType,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new BearerTokenResult
                {
                    // Should we append the type here?

                    AccessToken = AccessToken.Parse(clientToken.AccessToken.ToString())
                };
            }

            if (clientToken.DPoPJsonWebKey == null)
            {
                throw new InvalidOperationException("DPoP JsonWebKey was null");
            }

            return new DPoPTokenResult()
            {
                AccessToken = AccessToken.Parse(clientToken.AccessToken.ToString()),
                DPoPJsonWebKey = DPoPProofKey.Parse(clientToken.DPoPJsonWebKey!.ToString()!)
            };

            // Should we append the type here?
        }

        return new AccessTokenRetrievalError
        {
            Error = clientTokenFailure.Error,
            ErrorDescription = clientTokenFailure.ErrorDescription
        };
    }

    public static bool IsAjaxRequest(this HttpContext context)
    {
        if ("cors".Equals(context.Request.Headers["Sec-Fetch-Mode"].ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ("XMLHttpRequest".Equals(context.Request.Query["X-Requested-With"].ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ("XMLHttpRequest".Equals(context.Request.Headers["X-Requested-With"].ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
