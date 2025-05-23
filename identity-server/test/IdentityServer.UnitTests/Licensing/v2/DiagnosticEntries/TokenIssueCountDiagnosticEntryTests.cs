// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class TokenIssueCountDiagnosticEntryTests
{
    private readonly TokenIssueCountDiagnosticEntry _subject = new();

    [Fact]
    public async Task Should_Count_JwtAccessToken()
    {
        IssueToken(true, AccessTokenType.Jwt, false, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("Jwt").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_JwtReferenceToken()
    {
        IssueToken(true, AccessTokenType.Reference, false, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("Reference").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_JwtDPoPToken()
    {
        IssueToken(true, AccessTokenType.Jwt, false, ProofType.DPoP, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("JwtPoPDPoP").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_ReferenceDPoPToken()
    {
        IssueToken(true, AccessTokenType.Reference, false, ProofType.DPoP, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("ReferencePoPDPoP").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_JwtMTlsToken()
    {
        IssueToken(true, AccessTokenType.Jwt, false, ProofType.ClientCertificate, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("JwtPoPmTLS").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_ReferenceMTlsToken()
    {
        IssueToken(true, AccessTokenType.Reference, false, ProofType.ClientCertificate, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("ReferencePoPmTLS").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_RefreshToken()
    {
        IssueToken(false, AccessTokenType.Jwt, true, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("Refresh").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_IdToken()
    {
        IssueToken(false, AccessTokenType.Jwt, false, ProofType.None, true);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("Id").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Handle_Multiple_Token_Types()
    {
        IssueToken(true, AccessTokenType.Jwt, true, ProofType.None, false);
        IssueToken(true, AccessTokenType.Jwt, false, ProofType.DPoP, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        tokenIssueCounts.GetProperty("Jwt").GetInt64().ShouldBe(1);
        tokenIssueCounts.GetProperty("JwtPoPDPoP").GetInt64().ShouldBe(1);
        tokenIssueCounts.GetProperty("Refresh").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Ignore_Non_TokenIssued_Instruments()
    {
        Duende.IdentityServer.Telemetry.Metrics.TokenIssuedFailure("ClientId", "GrantType", null, "error");

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        tokenIssueCounts.GetProperty("Jwt").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("Reference").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("JwtPoPDPoP").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("JwtPoPmTLS").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("ReferencePoPDPoP").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("ReferencePoPmTLS").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("Refresh").GetInt64().ShouldBe(0);
    }

    private void IssueToken(bool accessTokenIssued, AccessTokenType accessTokenType, bool refreshTokenIssued,
        ProofType proofType, bool idTokenIssued) =>
        Duende.IdentityServer.Telemetry.Metrics.TokenIssued("ClientId", "GrantType", null, accessTokenIssued, accessTokenType, refreshTokenIssued, proofType, idTokenIssued);
}
