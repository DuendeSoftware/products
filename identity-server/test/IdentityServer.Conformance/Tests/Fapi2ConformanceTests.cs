// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Conformance.Infrastructure;

namespace Duende.IdentityServer.Conformance;

/// <summary>
/// End-to-end conformance tests for Duende IdentityServer against the OIDF FAPI 2.0
/// Security Profile certification test plan.
/// </summary>
[Collection("Fapi2ConformanceSuite")]
public sealed class Fapi2ConformanceTests(Fapi2ConformanceSuiteFixture fixture, ITestOutputHelper output)
    : ConformanceTestBase
{
    protected override ConformanceSuiteFixture Fixture => fixture;
    protected override ITestOutputHelper Output => output;
    protected override string CallbackUrlPrefix => "https://localhost:8443/test/a/duende-is-fapi2/callback";
    protected override string InternalHost => "nginx:8444";
    protected override string ExternalHost => "localhost:8444";

    [Fact] public async Task DiscoveryEndpointVerification() => await SimpleLogin("fapi2-security-profile-id2-discovery-end-point-verification");
    [Fact] public async Task HappyFlow() => await SimpleLogin("fapi2-security-profile-id2-happy-flow");
    [Fact] public async Task EnsureRequestObjectWithMultipleAudSucceeds() => await SimpleLogin("fapi2-security-profile-id2-ensure-request-object-with-multiple-aud-succeeds");
    [Fact] public async Task EnsureAuthorizationRequestWithoutStateSuccess() => await SimpleLogin("fapi2-security-profile-id2-ensure-authorization-request-without-state-success");
    [Fact] public async Task EnsureAuthorizationRequestWithoutNonceSuccess() => await SimpleLogin("fapi2-security-profile-id2-ensure-authorization-request-without-nonce-success");
    [Fact] public async Task EnsureRequestObjectWith64CharNonceSuccess() => await SimpleLogin("fapi2-security-profile-id2-ensure-request-object-with-64-char-nonce-success");
    [Fact] public async Task EnsureOtherScopeOrderSucceeds() => await SimpleLogin("fapi2-security-profile-id2-ensure-other-scope-order-succeeds");
    [Fact] public async Task TestClaimsParameterIdentityClaims() => await SimpleLogin("fapi2-security-profile-id2-test-claims-parameter-identity-claims");
    [Fact] public async Task AccessTokenTypeHeaderCaseSensitivity() => await SimpleLogin("fapi2-security-profile-id2-access-token-type-header-case-sensitivity");
    [Fact] public async Task CheckDpopProofNbfExp() => await SimpleLogin("fapi2-security-profile-id2-check-dpop-proof-nbf-exp");
    [Fact] public async Task EnsureDpopProofWithIat10SecondsBefore() => await SimpleLogin("fapi2-security-profile-id2-ensure-dpopproof-with-iat-10seconds-before-succeeds");
    [Fact] public async Task EnsureDpopProofWithIat10SecondsAfter() => await SimpleLogin("fapi2-security-profile-id2-ensure-dpopproof-with-iat-10seconds-after-succeeds");
    [Fact] public async Task EnsureMismatchedDpopJktFails() => await SimpleLogin("fapi2-security-profile-id2-ensure-mismatched-dpop-jkt-fails");
    [Fact] public async Task EnsureTokenEndpointFailsWithMismatchedDpopProofJkt() => await SimpleLogin("fapi2-security-profile-id2-ensure-token-endpoint-fails-with-mismatched-dpop-proof-jkt");
    [Fact] public async Task EnsureTokenEndpointFailsWithMismatchedDpopJkt() => await SimpleLogin("fapi2-security-profile-id2-ensure-token-endpoint-fails-with-mismatched-dpop-jkt");
    [Fact] public async Task EnsureDpopProofAtParEndpointBindingSuccess() => await SimpleLogin("fapi2-security-profile-id2-ensure-dpopproof-at-par-endpoint-binding-success");
    [Fact] public async Task EnsureDpopAuthCodeBindingSuccess() => await SimpleLogin("fapi2-security-profile-id2-ensure-dpop-auth-code-binding-success");
    [Fact] public async Task EnsureDifferentNonceInsideAndOutsideRequestObject() => await SimpleLogin("fapi2-security-profile-id2-ensure-different-nonce-inside-and-outside-request-object");
    [Fact] public async Task EnsureDifferentStateInsideAndOutsideRequestObject() => await SimpleLogin("fapi2-security-profile-id2-ensure-different-state-inside-and-outside-request-object");
    [Fact] public async Task EnsureRequestObjectWithLongNonce() => await SimpleLogin("fapi2-security-profile-id2-ensure-request-object-with-long-nonce");
    [Fact] public async Task EnsureRequestObjectWithLongState() => await SimpleLogin("fapi2-security-profile-id2-ensure-request-object-with-long-state");
    [Fact] public async Task StateOnlyOutsideRequestObjectNotUsed() => await SimpleLogin("fapi2-security-profile-id2-state-only-outside-request-object-not-used");
    [Fact] public async Task EnsureRequestObjectWithoutRedirectUriFails() => await SimpleLogin("fapi2-security-profile-id2-ensure-request-object-without-redirect-uri-fails");
    [Fact] public async Task PlainFapiEnsureRegisteredRedirectUri() => await SimpleLogin("fapi2-security-profile-id2-plain-fapi-ensure-registered-redirect-uri");
    [Fact] public async Task EnsureUnsignedAuthorizationRequestWithoutUsingParFails() => await AuthorizeErrorPage("fapi2-security-profile-id2-ensure-unsigned-authorization-request-without-using-par-fails");
    [Fact] public async Task EnsureRedirectUriInAuthorizationRequest() => await SimpleLogin("fapi2-security-profile-id2-ensure-redirect-uri-in-authorization-request");
    [Fact] public async Task EnsureResponseTypeCodeIdtokenFails() => await SimpleLogin("fapi2-security-profile-id2-ensure-response-type-code-idtoken-fails");
    [Fact] public async Task EnsureResponseTypeTokenFails() => await SimpleLogin("fapi2-security-profile-id2-ensure-response-type-token-fails");
    [Fact] public async Task EnsureClientIdInTokenEndpoint() => await SimpleLogin("fapi2-security-profile-id2-ensure-client-id-in-token-endpoint");
    [Fact] public async Task EnsureHolderOfKeyRequired() => await SimpleLogin("fapi2-security-profile-id2-ensure-holder-of-key-required");
    [Fact] public async Task EnsureAuthorizationCodeIsBoundToClient() => await SimpleLogin("fapi2-security-profile-id2-ensure-authorization-code-is-bound-to-client");
    [Fact] public async Task AttemptReuseAuthorizationCodeAfterOneSecond() => await SimpleLogin("fapi2-security-profile-id2-attempt-reuse-authorization-code-after-one-second");
    [Fact] public async Task EnsureSignedClientAssertionWithRS256Fails() => await SimpleLogin("fapi2-security-profile-id2-ensure-signed-client-assertion-with-RS256-fails");
    [Fact] public async Task EnsureClientAssertionInTokenEndpoint() => await SimpleLogin("fapi2-security-profile-id2-ensure-client-assertion-in-token-endpoint");
    [Fact] public async Task EnsureClientAssertionWithExpIs5MinutesInPastFails() => await SimpleLogin("fapi2-security-profile-id2-ensure-client-assertion-with-exp-is-5-minutes-in-past-fails");
    [Fact] public async Task EnsureClientAssertionWithWrongAudFails() => await SimpleLogin("fapi2-security-profile-id2-ensure-client-assertion-with-wrong-aud-fails");
    [Fact] public async Task EnsureClientAssertionWithNoSubFails() => await SimpleLogin("fapi2-security-profile-id2-ensure-client-assertion-with-no-sub-fails");
    [Fact] public async Task EnsureClientAssertionWithTokenEndpointAudSucceeds() => await SimpleLogin("fapi2-security-profile-id2-ensure-client-assertion-with-token-endpoint-aud-succeeds");
    [Fact] public async Task DpopNegativeTests() => await SimpleLogin("fapi2-security-profile-id2-dpop-negative-tests");
    [Fact] public async Task RefreshToken() => await SimpleLogin("fapi2-security-profile-id2-refresh-token");
    [Fact] public async Task ParTestPushedAuthorizationUrlAsAudienceForClientJwtAssertion() => await SimpleLogin("fapi2-security-profile-id2-par-test-pushed-authorization-url-as-audience-for-client-JWT-assertion");
    [Fact] public async Task ParEnsureJwtClientAssertionsNbf8SecondsInFutureIsAccepted() => await SimpleLogin("fapi2-security-profile-id2-par-ensure-jwt-client-assertions-nbf-8-seconds-in-the-future-is-accepted");
    [Fact] public async Task ParEnsureJwtClientAssertionsNbfOver60SecondsInFutureFails() => await SimpleLogin("fapi2-security-profile-id2-par-ensure-jwt-client-assertions-nbf-over-60-seconds-in-the-future-fails");
    [Fact] public async Task ParTestTokenEndpointUrlAsAudienceForClientJwtAssertion() => await SimpleLogin("fapi2-security-profile-id2-par-test-token-endpoint-url-as-audience-for-client-JWT-assertion");
    [Fact] public async Task TestArrayAsAudienceForClientJwtAssertion() => await SimpleLogin("fapi2-security-profile-id2-test-array-as-audience-for-client-JWT-assertion");
    [Fact] public async Task ParAttemptToUseRequestUriForDifferentClient() => await AuthorizeErrorPage("fapi2-security-profile-id2-par-attempt-to-use-request_uri-for-different-client");
    [Fact] public async Task ParAuthorizationRequestContainingRequestUriFormParam() => await SimpleLogin("fapi2-security-profile-id2-par-authorization-request-containing-request_uri-form-param");
    [Fact] public async Task ParAttemptInvalidHttpMethod() => await SimpleLogin("fapi2-security-profile-id2-par-attempt-invalid-http-method");
    [Fact] public async Task ParEnsurePkceRequired() => await SimpleLogin("fapi2-security-profile-id2-par-ensure-pkce-required");
    [Fact] public async Task EnsurePkceCodeVerifierRequired() => await SimpleLogin("fapi2-security-profile-id2-ensure-pkce-code-verifier-required");
    [Fact] public async Task IncorrectPkceCodeVerifierRejected() => await SimpleLogin("fapi2-security-profile-id2-incorrect-pkce-code-verifier-rejected");
    [Fact] public async Task ParPlainPkceRejected() => await SimpleLogin("fapi2-security-profile-id2-par-plain-pkce-rejected");
    [Fact] public async Task ParWithoutDuplicateParameters() => await SimpleLogin("fapi2-security-profile-id2-par-without-duplicate-parameters");

    [Fact]
    public async Task EnsureTokenEndpointFailsWithExpiredAuthCode()
    {
        await fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync("fapi2-security-profile-id2-ensure-token-endpoint-fails-with-expired-auth-code");
        try
        {
            // The module sends an authorize request which we complete normally
            var step = await NextStep(moduleId);
            await Authorize(moduleId, step);

            // The module then waits for the auth code to expire (60s) before attempting
            // to exchange it at the token endpoint
            output.WriteLine("Waiting 65 seconds for auth code to expire...");
            await Task.Delay(TimeSpan.FromSeconds(65));

            // Suite verifies the token endpoint rejects the expired code
            await AssertPassedAsync(moduleId);
        }
        catch
        {
            await CaptureModuleDetailsAsync(moduleId);
            throw;
        }
    }

    [Fact]
    public async Task ParAttemptToUseExpiredRequestUri()
    {
        var moduleId = await StartModuleAsync("fapi2-security-profile-id2-par-attempt-to-use-expired-request_uri");

        // The module pushes a PAR request, then waits for the request_uri to expire (60s)
        // before sending the browser to the authorize endpoint with the expired request_uri.
        output.WriteLine("Waiting 65 seconds for PAR request_uri to expire...");
        await Task.Delay(TimeSpan.FromSeconds(65));

        // Now the module should have a browser URL ready with the expired request_uri
        var step = await NextStep(moduleId);
        await AssertErrorPageAsync(moduleId, step);
    }

    [Fact]
    public async Task ParAttemptReuseRequestUri()
    {
        var moduleId = await StartModuleAsync("fapi2-security-profile-id2-par-attempt-reuse-request_uri");

        // First, the module pushes authorization request params

        // Then it sends an authorize request using the PAR request_uri for the first time, which succeeds
        var step = await NextStep(moduleId);
        await Authorize(moduleId, step);

        // Then it sends an authorize request attempting to reuse the PAR request_uri, which fails
        step = await NextStep(moduleId);
        await AssertErrorPageAsync(moduleId, step);
    }

    [Fact]
    public async Task UserRejectsAuthentication()
    {
        var moduleId = await StartModuleAsync("fapi2-security-profile-id2-user-rejects-authentication");

        // Module sends an authorize request, which we deny
        var step = await NextStep(moduleId);
        await DenyAuthorization(moduleId, step);

        // Same thing, for second client
        step = await NextStep(moduleId);
        await DenyAuthorization(moduleId, step);

        // Suite verifies both clients received access_denied
        await AssertPassedAsync(moduleId);
    }

    [Fact]
    public async Task ParEnsureReusedRequestUriPriorToAuthCompletionSucceeds()
    {
        await fixture.LoginAutomation.ResetContextAsync();
        var moduleId = await StartModuleAsync("fapi2-security-profile-id2-par-ensure-reused-request-uri-prior-to-auth-completion-succeeds");

        // First, module sends an authorize request using a PAR request_uri.

        // We navigate to the login page but DON'T complete auth
        var step = await NextStep(moduleId);
        var s = step.ShouldBeOfType<ConformanceUrlStep>();
        s.Kind.ShouldBe(ConformanceStepKind.Authorize);
        var (pendingPage, pendingContext) = await fixture.LoginAutomation.NavigateToLoginPageAsync(
            s.Url, timeout: TimeSpan.FromSeconds(30), log: msg => output.WriteLine(msg));
        await pendingPage.CloseAsync();
        await pendingContext.CloseAsync();
        await fixture.Client.AcknowledgeBrowserUrlAsync(moduleId, s);

        // Module reuses the same request_uri. This time we complete auth normally.
        // We pre-acknowledge the visit because the suite's processCallback() checks
        // that the authorize URL was visited >= 2 times before the callback arrives.
        step = await NextStep(moduleId);
        s = step.ShouldBeOfType<ConformanceUrlStep>();
        s.Kind.ShouldBe(ConformanceStepKind.Authorize);
        await fixture.Client.AcknowledgeBrowserUrlAsync(moduleId, s);
        await fixture.LoginAutomation.CompleteAuthorizationAsync(
            s.Url, CallbackUrlPrefix, log: msg => output.WriteLine(msg));

        await AssertPassedAsync(moduleId, step);
    }

    /// <summary>
    /// For tests where IS shows an error page (invalid redirect_uri, expired PAR, etc.).
    /// </summary>
    private async Task AuthorizeErrorPage(string moduleName)
    {
        var moduleId = await StartModuleAsync(moduleName);
        try
        {
            var step = await NextStep(moduleId);
            await AssertErrorPageAsync(moduleId, step);
        }
        catch
        {
            await CaptureModuleDetailsAsync(moduleId);
            throw;
        }
    }
}
