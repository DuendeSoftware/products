// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Hosting;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(CORSRequestMadeForPathPathFromOrigin),
        Message = "CORS request made for path: {Path} from origin: {Origin}")]
    internal static partial void CORSRequestMadeForPathPathFromOrigin(this ILogger logger, object path, object origin);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(CorsPolicyServiceAllowedOriginOrigin),
        Message = "CorsPolicyService allowed origin: {Origin}")]
    internal static partial void CorsPolicyServiceAllowedOriginOrigin(this ILogger logger, object origin);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(CorsPolicyServiceDidNotAllowOriginOrigin),
        Message = "CorsPolicyService did not allow origin: {Origin}")]
    internal static partial void CorsPolicyServiceDidNotAllowOriginOrigin(this ILogger logger, object origin);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(IdentityServerCorsPolicyServiceDidnTHandleCORSRequestMade),
        Message = "IdentityServer CorsPolicyService didn't handle CORS request made for path: {Path} from origin: {Origin} " +
                                          "because it is not for an IdentityServer CORS endpoint. To allow CORS requests to non IdentityServer endpoints, please " +
                                          "set up your own Cors policy for your application by calling app.UseCors(\"MyPolicy\") in the pipeline setup.")]
    internal static partial void IdentityServerCorsPolicyServiceDidnTHandleCORSRequestMade(this ILogger logger, object path, object origin);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(RequestPathPathMatchedToEndpointTypeEndpoint),
        Message = "Request path {Path} matched to endpoint type {Endpoint}")]
    internal static partial void RequestPathPathMatchedToEndpointTypeEndpoint(this ILogger logger, object path, object endpoint);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(NoEndpointEntryFoundForRequestPathPath),
        Message = "No endpoint entry found for request path: {Path}")]
    internal static partial void NoEndpointEntryFoundForRequestPathPath(this ILogger logger, object path);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(EndpointEnabledEndpointSuccessfullyCreatedHandlerEndpointHandler),
        Message = "Endpoint enabled: {Endpoint}, successfully created handler: {EndpointHandler}")]
    internal static partial void EndpointEnabledEndpointSuccessfullyCreatedHandlerEndpointHandler(this ILogger logger, string endpoint, string endpointHandler);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(EndpointEnabledEndpointFailedToCreateHandlerEndpointHandler),
        Message = "Endpoint enabled: {Endpoint}, failed to create handler: {EndpointHandler}")]
    internal static partial void EndpointEnabledEndpointFailedToCreateHandlerEndpointHandler(this ILogger logger, string endpoint, string endpointHandler);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(EndpointDisabledEndpoint),
        Message = "Endpoint disabled: {Endpoint}")]
    internal static partial void EndpointDisabledEndpoint(this ILogger logger, string endpoint);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(AugmentingSignInContext),
        Message = "Augmenting SignInContext")]
    internal static partial void AugmentingSignInContext(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(SignOutCalledSetProcessingPostSignoutSessionCleanup),
        Message = "SignOutCalled set; processing post-signout session cleanup.")]
    internal static partial void SignOutCalledSetProcessingPostSignoutSessionCleanup(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(RemovingAmrClaimWithValueValue),
        Message = "Removing amr claim with value: {Value}")]
    internal static partial void RemovingAmrClaimWithValueValue(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(AddingIdpClaimWithValueValue),
        Message = "Adding idp claim with value: {Value}")]
    internal static partial void AddingIdpClaimWithValueValue(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(AddingAmrClaimWithValueValue),
        Message = "Adding amr claim with value: {Value}")]
    internal static partial void AddingAmrClaimWithValueValue(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(AddingIdpClaimWithValueValue2),
        Message = "Adding idp claim with value: {Value}")]
    internal static partial void AddingIdpClaimWithValueValue2(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(AddingAmrClaimWithValueValue2),
        Message = "Adding amr claim with value: {Value}")]
    internal static partial void AddingAmrClaimWithValueValue2(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(AddingAmrClaimWithValueValue3),
        Message = "Adding amr claim with value: {Value}")]
    internal static partial void AddingAmrClaimWithValueValue3(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(AddingAuthTimeClaimWithValueValue),
        Message = "Adding auth_time claim with value: {Value}")]
    internal static partial void AddingAuthTimeClaimWithValueValue(this ILogger logger, object value);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(DetectedExpiredSessionRemovedProcessingPostExpirationCleanup),
        Message = "Detected expired session removed; processing post-expiration cleanup.")]
    internal static partial void DetectedExpiredSessionRemovedProcessingPostExpirationCleanup(this ILogger logger);

    [LoggerMessage(
        LogLevel.Information,
        EventName = nameof(InvokingIdentityServerEndpointEndpointTypeForUrl),
        Message = "Invoking IdentityServer endpoint: {EndpointType} for {Url}")]
    internal static partial void InvokingIdentityServerEndpointEndpointTypeForUrl(this ILogger logger, string endpointType, object url);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(InvokingResultType),
        Message = "Invoking result: {Type}")]
    internal static partial void InvokingResultType(this ILogger logger, string type);

    [LoggerMessage(
        LogLevel.Critical,
        EventName = nameof(UnhandledExceptionException),
        Message = "Unhandled exception: {Exception}")]
    internal static partial void UnhandledExceptionException(this ILogger logger, System.Exception caughtException, object exception);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(RequiringMTLSBecauseTheRequestSDomainMatches),
        Message = "Requiring mTLS because the request's domain matches the configured mTLS domain name.")]
    internal static partial void RequiringMTLSBecauseTheRequestSDomainMatches(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(RequiringMTLSBecauseTheRequestSSubdomainMatches),
        Message = "Requiring mTLS because the request's subdomain matches the configured mTLS domain name.")]
    internal static partial void RequiringMTLSBecauseTheRequestSSubdomainMatches(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(NotRequiringMTLSBecauseThisRequestSDomain),
        Message = "Not requiring mTLS because this request's domain does not match the configured mTLS domain name.")]
    internal static partial void NotRequiringMTLSBecauseThisRequestSDomain(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(RequiringMTLSBecauseTheRequestSPathBegins),
        Message = "Requiring mTLS because the request's path begins with the configured mTLS path prefix.")]
    internal static partial void RequiringMTLSBecauseTheRequestSPathBegins(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(RewritingMTLSRequestFromOldPathToNewPath),
        Message = "Rewriting MTLS request from: {OldPath} to: {NewPath}")]
    internal static partial void RewritingMTLSRequestFromOldPathToNewPath(this ILogger logger, object oldPath, object newPath);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(MTLSAuthenticationFailedErrorError),
        Message = "MTLS authentication failed, error: {Error}.")]
    internal static partial void MTLSAuthenticationFailedErrorError(this ILogger logger, object error);
}
