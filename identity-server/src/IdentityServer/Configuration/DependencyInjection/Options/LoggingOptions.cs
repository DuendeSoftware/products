// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using Duende.IdentityModel;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Settings for filtering sensitive parameter values from logs and suppressing noisy unhandled
/// exceptions.
/// </summary>
public class LoggingOptions
{
    /// <summary>
    /// Gets or sets the parameter names whose values are redacted from backchannel authentication (CIBA) request
    /// log entries.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>client_secret</c>, <c>client_assertion</c>, <c>id_token_hint</c>, and
    /// <c>request</c>. Clearing or replacing this collection may expose sensitive values in logs.
    /// </remarks>
    public ICollection<string> BackchannelAuthenticationRequestSensitiveValuesFilter { get; set; } =
        new HashSet<string>
        {
            OidcConstants.TokenRequest.ClientSecret,
            OidcConstants.TokenRequest.ClientAssertion,
            OidcConstants.AuthorizeRequest.IdTokenHint,
            OidcConstants.AuthorizeRequest.Request
        };

    /// <summary>
    /// Gets or sets the parameter names whose values are redacted from token endpoint request log entries.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>client_secret</c>, <c>password</c>, <c>client_assertion</c>,
    /// <c>refresh_token</c>, <c>device_code</c>, <c>code</c>, and <c>subject_token</c>.
    /// Clearing or replacing this collection may expose sensitive values in logs.
    /// </remarks>
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
    /// Gets or sets the parameter names whose values are redacted from authorize endpoint request log entries.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>client_secret</c>, <c>client_assertion</c>, <c>id_token_hint</c>, and
    /// <c>request</c>. Because authorization parameters pushed via PAR are eventually processed
    /// by the authorize endpoint pipeline, this filter should typically be kept in sync with
    /// <see cref="PushedAuthorizationSensitiveValuesFilter"/>. Clearing or replacing this
    /// collection may expose sensitive values in logs.
    /// </remarks>
    public ICollection<string> AuthorizeRequestSensitiveValuesFilter { get; set; } =
        new HashSet<string>
        {
            // Secrets and assertions may be passed to the authorize endpoint via PAR
            OidcConstants.TokenRequest.ClientSecret,
            OidcConstants.TokenRequest.ClientAssertion,
            OidcConstants.AuthorizeRequest.IdTokenHint,
            OidcConstants.AuthorizeRequest.Request
        };

    /// <summary>
    /// Gets or sets the parameter names whose values are redacted from Pushed Authorization Request (PAR)
    /// endpoint log entries.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>client_secret</c>, <c>client_assertion</c>, <c>id_token_hint</c>, and
    /// <c>request</c>. Because pushed authorization parameters are eventually processed by the
    /// authorize endpoint pipeline, this filter should typically be kept in sync with
    /// <see cref="AuthorizeRequestSensitiveValuesFilter"/>. Clearing or replacing this
    /// collection may expose sensitive values in logs.
    /// </remarks>
    public ICollection<string> PushedAuthorizationSensitiveValuesFilter { get; set; } =
        new HashSet<string>
        {
            OidcConstants.TokenRequest.ClientSecret,
            OidcConstants.TokenRequest.ClientAssertion,
            OidcConstants.AuthorizeRequest.IdTokenHint,
            OidcConstants.AuthorizeRequest.Request
        };

    /// <summary>
    /// Gets or sets a predicate invoked when the IdentityServer middleware detects an unhandled exception,
    /// used to decide whether the exception should be logged. Return <c>true</c> to emit the
    /// log entry, or <c>false</c> to suppress it.
    /// </summary>
    /// <remarks>
    /// By default, <see cref="OperationCanceledException"/> instances are suppressed when the
    /// request's <c>CancellationToken</c> has been cancelled, because these exceptions are an
    /// expected consequence of HTTP request cancellation and would otherwise create unnecessary
    /// log noise.
    /// </remarks>
    public Func<HttpContext, Exception, bool> UnhandledExceptionLoggingFilter = (context, exception) =>
    {
        var result = !(context.RequestAborted.IsCancellationRequested && exception is OperationCanceledException);
        return result;
    };

    internal bool InvokeUnhandledExceptionLoggingFilter(HttpContext context, Exception exception)
    {
        if (UnhandledExceptionLoggingFilter == null)
        {
            return true;
        }

        var list = UnhandledExceptionLoggingFilter
            .GetInvocationList()
            .Cast<Func<HttpContext, Exception, bool>>();

        return list.Aggregate(true,
            (current, filter) =>
                current && filter.Invoke(context, exception));
    }
}
