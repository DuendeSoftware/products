// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Duende.IdentityModel;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Options for configuring logging behavior
/// </summary>
public class LoggingOptions
{
    /// <summary>
    /// Gets or sets the collection of keys that will be used to redact sensitive values from a backchannel authentication request log.
    /// </summary>
    /// <remarks>Please be aware that initializing this property could expose sensitive information in your logs.</remarks>
    public ICollection<string> BackchannelAuthenticationRequestSensitiveValuesFilter { get; set; } =
        new HashSet<string>
        {
            OidcConstants.TokenRequest.ClientSecret,
            OidcConstants.TokenRequest.ClientAssertion,
            OidcConstants.AuthorizeRequest.IdTokenHint
        };

    /// <summary>
    /// Gets or sets the collection of keys that will be used to redact sensitive values from a token request log.
    /// </summary>
    /// <remarks>Please be aware that initializing this property could expose sensitive information in your logs.</remarks>
    public ICollection<string> TokenRequestSensitiveValuesFilter { get; set; } = 
        new HashSet<string>
        {
            OidcConstants.TokenRequest.ClientSecret,
            OidcConstants.TokenRequest.Password,
            OidcConstants.TokenRequest.ClientAssertion,
            OidcConstants.TokenRequest.RefreshToken,
            OidcConstants.TokenRequest.DeviceCode,
            OidcConstants.TokenRequest.Code,
            OidcConstants.TokenRequest.SubjectToken
        };

    /// <summary>
    /// Gets or sets the collection of keys that will be used to redact sensitive values from an authorize request log.
    /// </summary>
    /// <remarks>Please be aware that initializing this property could expose sensitive information in your logs.</remarks>
    public ICollection<string> AuthorizeRequestSensitiveValuesFilter { get; set; } = 
        new HashSet<string>
        {
            // Secrets and assertions may be passed to the authorize endpoint via PAR
            OidcConstants.TokenRequest.ClientSecret,
            OidcConstants.TokenRequest.ClientAssertion,
            OidcConstants.AuthorizeRequest.IdTokenHint
        };

    /// <summary>
    /// Gets or sets the collection of keys that will be used to redact sensitive values from a pushed authorization request log.
    /// </summary>
    /// <remarks>Please be aware that initializing this property could expose sensitive information in your logs.</remarks>
    /// <remarks>Note that pushed authorization parameters are eventually handled by the authorize request pipeline.
    /// In most cases, changes to this collection should also be made to <see cref="AuthorizeRequestSensitiveValuesFilter"/>
    /// </remarks>
    public ICollection<string> PushedAuthorizationSensitiveValuesFilter { get; set; } =
        new HashSet<string>
        {
            OidcConstants.TokenRequest.ClientSecret,
            OidcConstants.TokenRequest.ClientAssertion,
            OidcConstants.AuthorizeRequest.IdTokenHint
        };

    /// <summary>
    /// Called when the IdentityServer middleware detects an unhandled exception, and is used to determine if the exception is logged.
    /// Returns true to emit the log, false to suppress.
    /// </summary>
    public Func<HttpContext, Exception, bool> UnhandledExceptionLoggingFilter = (context, exception) =>
    {
        var result = !(context.RequestAborted.IsCancellationRequested && exception is OperationCanceledException);
        return result;
    };
    
}
