// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#pragma warning disable 1591

namespace Duende.IdentityServer.Events;

public static class EventIds
{
    //////////////////////////////////////////////////////
    /// Authentication related events
    //////////////////////////////////////////////////////
    private const int AuthenticationEventsStart = 1000;

    public const int UserLoginSuccess = AuthenticationEventsStart + 0;
    public const int UserLoginFailure = AuthenticationEventsStart + 1;
    public const int UserLogoutSuccess = AuthenticationEventsStart + 2;

    public const int ClientAuthenticationSuccess = AuthenticationEventsStart + 10;
    public const int ClientAuthenticationFailure = AuthenticationEventsStart + 11;

    public const int ApiAuthenticationSuccess = AuthenticationEventsStart + 20;
    public const int ApiAuthenticationFailure = AuthenticationEventsStart + 21;

    //////////////////////////////////////////////////////
    /// Token related events
    //////////////////////////////////////////////////////
    private const int TokenEventsStart = 2000;

    public const int TokenIssuedSuccess = TokenEventsStart + 0;
    public const int TokenIssuedFailure = TokenEventsStart + 1;

    public const int TokenRevokedSuccess = TokenEventsStart + 10;

    public const int TokenIntrospectionSuccess = TokenEventsStart + 20;
    public const int TokenIntrospectionFailure = TokenEventsStart + 21;

    //////////////////////////////////////////////////////
    /// Error related events
    //////////////////////////////////////////////////////
    private const int ErrorEventsStart = 3000;

    public const int UnhandledException = ErrorEventsStart + 0;
    public const int InvalidClientConfiguration = ErrorEventsStart + 1;
    public const int InvalidIdentityProviderConfiguration = ErrorEventsStart + 2;

    //////////////////////////////////////////////////////
    /// Grants related events
    //////////////////////////////////////////////////////
    private const int GrantsEventsStart = 4000;

    public const int ConsentGranted = GrantsEventsStart + 0;
    public const int ConsentDenied = GrantsEventsStart + 1;
    public const int GrantsRevoked = GrantsEventsStart + 2;

    //////////////////////////////////////////////////////
    /// Device flow related events
    //////////////////////////////////////////////////////
    private const int DeviceFlowEventsStart = 5000;

    public const int DeviceAuthorizationSuccess = DeviceFlowEventsStart + 0;
    public const int DeviceAuthorizationFailure = DeviceFlowEventsStart + 1;

    //////////////////////////////////////////////////////
    /// Backchannel authentication related events
    //////////////////////////////////////////////////////
    private const int BackchannelAuthenticationEventsStart = 6000;

    public const int BackchannelAuthenticationSuccess = BackchannelAuthenticationEventsStart + 0;
    public const int BackchannelAuthenticationFailure = BackchannelAuthenticationEventsStart + 1;

    //////////////////////////////////////////////////////
    /// Diagnostics related events
    //////////////////////////////////////////////////////
    private const int DiagnosticEventsStart = 7000;

    public const int DiagnosticSummaryLogged = DiagnosticEventsStart + 0;
}
