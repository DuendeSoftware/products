// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Reflection;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Duende.IdentityServer.Models;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class TokenIssueCountDiagnosticEntryTests
{
    private readonly TokenIssueCountDiagnosticEntry _subject = new();

    [Fact]
    public async Task Should_Count_JwtAccessToken()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("Jwt").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_JwtReferenceToken()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Reference, false, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("Reference").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_JwtDPoPToken()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.DPoP, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("JwtDPoP").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_ReferenceDPoPToken()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Reference, false, ProofType.DPoP, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("ReferenceDPoP").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_JwtMTlsToken()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.ClientCertificate, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("JwtMTLS").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_ReferenceMTlsToken()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Reference, false, ProofType.ClientCertificate, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("ReferenceMTLS").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_RefreshToken()
    {
        IssueToken("refresh_token", false, AccessTokenType.Jwt, true, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("Refresh").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_IdToken()
    {
        IssueToken(GrantType.AuthorizationCode, false, AccessTokenType.Jwt, false, ProofType.None, true);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("Id").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Handle_Multiple_Token_Types()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, true, ProofType.None, false);
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.DPoP, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        tokenIssueCounts.GetProperty("Jwt").GetInt64().ShouldBe(1);
        tokenIssueCounts.GetProperty("JwtDPoP").GetInt64().ShouldBe(1);
        tokenIssueCounts.GetProperty("Refresh").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Handle_No_Token_Issued()
    {
        IssueToken(GrantType.AuthorizationCode, false, null, false, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        tokenIssueCounts.GetProperty("Jwt").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("Reference").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("JwtDPoP").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("JwtMTLS").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("ReferenceDPoP").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("ReferenceMTLS").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("Refresh").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("Id").GetInt64().ShouldBe(0);
    }

    [Fact]
    public async Task Should_Handle_Initial_Grant_Type_Count()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        tokenIssueCounts.GetProperty(GrantType.AuthorizationCode).GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Handle_Multiple_Grant_Type_Counts()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.None, false);
        IssueToken(GrantType.ClientCredentials, true, AccessTokenType.Jwt, false, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        tokenIssueCounts.GetProperty(GrantType.AuthorizationCode).GetInt64().ShouldBe(1);
        tokenIssueCounts.GetProperty(GrantType.ClientCredentials).GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Handle_Multiple_Grant_Type_Counts_With_Grant_Type()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.None, false);
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        tokenIssueCounts.GetProperty(GrantType.AuthorizationCode).GetInt64().ShouldBe(2);
    }

    [Fact]
    public async Task Should_Handle_Grant_Type_Counts_For_All_Grant_Types()
    {
        var grantTypes = typeof(GrantType).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && !field.IsInitOnly)
            .Select(field => field.GetValue(null)?.ToString())
            .Where(value => value != null);
        foreach (var grantType in grantTypes)
        {
            IssueToken(grantType, true, AccessTokenType.Jwt, false, ProofType.None, false);
        }

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        foreach (var grantType in grantTypes)
        {
            tokenIssueCounts.GetProperty(grantType).GetInt64().ShouldBe(1);
        }
    }

    [Fact]
    public async Task Should_Ignore_Non_TokenIssued_Instruments()
    {
        Duende.IdentityServer.Telemetry.Metrics.TokenIssuedFailure("ClientId", "GrantType", null, "error");

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        tokenIssueCounts.GetProperty("Jwt").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("Reference").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("JwtDPoP").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("JwtMTLS").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("ReferenceDPoP").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("ReferenceMTLS").GetInt64().ShouldBe(0);
        tokenIssueCounts.GetProperty("Refresh").GetInt64().ShouldBe(0);
    }

    private void IssueToken(string grantType, bool accessTokenIssued, AccessTokenType? accessTokenType, bool refreshTokenIssued,
        ProofType proofType, bool idTokenIssued) =>
        Duende.IdentityServer.Telemetry.Metrics.TokenIssued("ClientId", grantType, null, accessTokenIssued, accessTokenType, refreshTokenIssued, proofType, idTokenIssued);
}
