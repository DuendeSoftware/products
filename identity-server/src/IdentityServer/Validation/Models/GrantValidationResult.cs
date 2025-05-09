// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Security.Claims;
using Duende.IdentityModel;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;

namespace Duende.IdentityServer.Validation;

/// <summary>
/// Models the result of custom grant validation.
/// </summary>
public class GrantValidationResult : ValidationResult
{
    /// <summary>
    /// Gets or sets the principal which represents the result of the validation.
    /// </summary>
    /// <value>
    /// The principal.
    /// </value>
    public ClaimsPrincipal Subject { get; set; }

    /// <summary>
    /// Custom fields for the token response
    /// </summary>
    public Dictionary<string, object> CustomResponse { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Initializes a new instance of the <see cref="GrantValidationResult"/> class with no subject.
    /// Warning: the resulting access token will only contain the client identity.
    /// </summary>
    public GrantValidationResult(Dictionary<string, object> customResponse = null)
    {
        IsError = false;
        CustomResponse = customResponse;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GrantValidationResult"/> class with a given principal.
    /// Warning: the principal needs to include the required claims - it is recommended to use the other constructor that does validation.
    /// </summary>
    public GrantValidationResult(ClaimsPrincipal principal, Dictionary<string, object> customResponse = null)
    {
        IsError = false;

        if (principal.Identities.Count() != 1)
        {
            throw new InvalidOperationException("only a single identity supported");
        }

        if (principal.FindFirst(JwtClaimTypes.Subject) == null)
        {
            throw new InvalidOperationException("sub claim is missing");
        }

        if (principal.FindFirst(JwtClaimTypes.IdentityProvider) == null)
        {
            throw new InvalidOperationException("idp claim is missing");
        }

        if (principal.FindFirst(JwtClaimTypes.AuthenticationMethod) == null)
        {
            throw new InvalidOperationException("amr claim is missing");
        }

        if (principal.FindFirst(JwtClaimTypes.AuthenticationTime) == null)
        {
            throw new InvalidOperationException("auth_time claim is missing");
        }

        Subject = principal;
        CustomResponse = customResponse;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GrantValidationResult"/> class with an error and description.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="errorDescription">The error description.</param>
    /// <param name="customResponse">Custom response elements</param>
    public GrantValidationResult(TokenRequestErrors error, string errorDescription = null, Dictionary<string, object> customResponse = null)
    {
        Error = ConvertTokenErrorEnumToString(error);
        ErrorDescription = errorDescription;
        CustomResponse = customResponse;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GrantValidationResult" /> class.
    /// </summary>
    /// <param name="subject">The subject claim used to uniquely identifier the user.</param>
    /// <param name="authenticationMethod">The authentication method which describes the custom grant type.</param>
    /// <param name="claims">Additional claims that will be maintained in the principal. 
    /// If you want these claims to appear in token, you need to add them explicitly in your custom implementation of <see cref="Services.IProfileService"/> service.</param>
    /// <param name="identityProvider">The identity provider.</param>
    /// <param name="customResponse">The custom response.</param>
    public GrantValidationResult(
        string subject,
        string authenticationMethod,
        IEnumerable<Claim> claims = null,
        string identityProvider = IdentityServerConstants.LocalIdentityProvider,
        Dictionary<string, object> customResponse = null)
        : this(subject, authenticationMethod, DateTime.UtcNow, claims, identityProvider, customResponse)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GrantValidationResult" /> class.
    /// </summary>
    /// <param name="subject">The subject claim used to uniquely identifier the user.</param>
    /// <param name="authenticationMethod">The authentication method which describes the custom grant type.</param>
    /// <param name="authTime">The UTC date/time of authentication.</param>
    /// <param name="claims">Additional claims that will be maintained in the principal.
    /// If you want these claims to appear in token, you need to add them explicitly in your custom implementation of <see cref="Services.IProfileService"/> service.</param>
    /// <param name="identityProvider">The identity provider.</param>
    /// <param name="customResponse">The custom response.</param>
    public GrantValidationResult(
        string subject,
        string authenticationMethod,
        DateTime authTime,
        IEnumerable<Claim> claims = null,
        string identityProvider = IdentityServerConstants.LocalIdentityProvider,
        Dictionary<string, object> customResponse = null)
    {
        IsError = false;

        var resultClaims = new List<Claim>
        {
            new Claim(JwtClaimTypes.Subject, subject),
            new Claim(JwtClaimTypes.AuthenticationMethod, authenticationMethod),
            new Claim(JwtClaimTypes.IdentityProvider, identityProvider),
            new Claim(JwtClaimTypes.AuthenticationTime, new DateTimeOffset(authTime).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        if (!IEnumerableExtensions.IsNullOrEmpty(claims))
        {
            resultClaims.AddRange(claims);
        }

        var id = new ClaimsIdentity(authenticationMethod);
        id.AddClaims(resultClaims.Distinct(new ClaimComparer()));

        Subject = new ClaimsPrincipal(id);
        CustomResponse = customResponse;
    }

    private string ConvertTokenErrorEnumToString(TokenRequestErrors error) => error switch
    {
        TokenRequestErrors.InvalidClient => OidcConstants.TokenErrors.InvalidClient,
        TokenRequestErrors.InvalidGrant => OidcConstants.TokenErrors.InvalidGrant,
        TokenRequestErrors.InvalidRequest => OidcConstants.TokenErrors.InvalidRequest,
        TokenRequestErrors.InvalidScope => OidcConstants.TokenErrors.InvalidScope,
        TokenRequestErrors.UnauthorizedClient => OidcConstants.TokenErrors.UnauthorizedClient,
        TokenRequestErrors.UnsupportedGrantType => OidcConstants.TokenErrors.UnsupportedGrantType,
        TokenRequestErrors.InvalidTarget => OidcConstants.TokenErrors.InvalidTarget,
        _ => throw new InvalidOperationException("invalid token error")
    };
}
