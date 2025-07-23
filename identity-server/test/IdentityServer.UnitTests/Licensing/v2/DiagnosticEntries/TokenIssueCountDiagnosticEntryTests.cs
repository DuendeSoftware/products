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

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("AccessTokensByType").GetProperty("Jwt").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_JwtReferenceToken()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Reference, false, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("AccessTokensByType").GetProperty("Reference").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_DPoP_Constraint_For_DPoP_Constrained_JWT()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.DPoP, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("AccessTokensBySenderConstraint").GetProperty("DPoP").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_DPoP_Constraint_For_DPoP_Constrained_Reference_Token()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Reference, false, ProofType.DPoP, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("AccessTokensBySenderConstraint").GetProperty("DPoP").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_mTLS_Constraint_For_mTLS_Constrained_JWT()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.ClientCertificate, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("AccessTokensBySenderConstraint").GetProperty("mTLS").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_mTLS_Constraint_For_mTLS_Constrained_Reference_Token()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Reference, false, ProofType.ClientCertificate, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("AccessTokensBySenderConstraint").GetProperty("mTLS").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_Refresh_Token()
    {
        IssueToken("refresh_token", false, AccessTokenType.Jwt, true, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("TokensByType").GetProperty("Refresh").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Count_Id_Token()
    {
        IssueToken(GrantType.AuthorizationCode, false, AccessTokenType.Jwt, false, ProofType.None, true);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        result.RootElement.GetProperty("TokenIssueCounts").GetProperty("TokensByType").GetProperty("Id").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Handle_Multiple_Token_Types()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, true, ProofType.None, false);
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.DPoP, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        tokenIssueCounts.GetProperty("AccessTokensByType").GetProperty("Jwt").GetInt64().ShouldBe(2);
        var senderConstraint = tokenIssueCounts.GetProperty("AccessTokensBySenderConstraint");
        senderConstraint.GetProperty("None").GetInt64().ShouldBe(1);
        senderConstraint.GetProperty("DPoP").GetInt64().ShouldBe(1);
        tokenIssueCounts.GetProperty("TokensByType").GetProperty("Refresh").GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Handle_No_Token_Issued()
    {
        IssueToken(GrantType.AuthorizationCode, false, null, false, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        var accessTokensByType = tokenIssueCounts.GetProperty("AccessTokensByType");
        accessTokensByType.GetProperty("Jwt").GetInt64().ShouldBe(0);
        accessTokensByType.GetProperty("Reference").GetInt64().ShouldBe(0);
        var senderConstraint = tokenIssueCounts.GetProperty("AccessTokensBySenderConstraint");
        senderConstraint.GetProperty("None").GetInt64().ShouldBe(0);
        senderConstraint.GetProperty("DPoP").GetInt64().ShouldBe(0);
        senderConstraint.GetProperty("mTLS").GetInt64().ShouldBe(0);
        var tokensByType = tokenIssueCounts.GetProperty("TokensByType");
        tokensByType.GetProperty("Access").GetInt64().ShouldBe(0);
        tokensByType.GetProperty("Refresh").GetInt64().ShouldBe(0);
        tokensByType.GetProperty("Id").GetInt64().ShouldBe(0);
    }

    [Fact]
    public async Task Should_Handle_Initial_Grant_Type_Count()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        tokenIssueCounts.GetProperty("RequestsByGrantType").GetProperty(GrantType.AuthorizationCode).GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Handle_Multiple_Grant_Type_Counts()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.None, false);
        IssueToken(GrantType.ClientCredentials, true, AccessTokenType.Jwt, false, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        var grantTypeCounts = tokenIssueCounts.GetProperty("RequestsByGrantType");
        grantTypeCounts.GetProperty(GrantType.AuthorizationCode).GetInt64().ShouldBe(1);
        grantTypeCounts.GetProperty(GrantType.ClientCredentials).GetInt64().ShouldBe(1);
    }

    [Fact]
    public async Task Should_Handle_Multiple_Grant_Type_Counts_With_Grant_Type()
    {
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.None, false);
        IssueToken(GrantType.AuthorizationCode, true, AccessTokenType.Jwt, false, ProofType.None, false);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        tokenIssueCounts.GetProperty("RequestsByGrantType").GetProperty(GrantType.AuthorizationCode).GetInt64().ShouldBe(2);
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
        var grantTypeCounts = tokenIssueCounts.GetProperty("RequestsByGrantType");
        foreach (var grantType in grantTypes)
        {
            grantTypeCounts.GetProperty(grantType).GetInt64().ShouldBe(1);
        }
    }

    [Fact]
    public async Task Should_Ignore_Non_TokenIssued_Instruments()
    {
        Duende.IdentityServer.Telemetry.Metrics.TokenIssuedFailure("ClientId", "GrantType", null, "error");

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var tokenIssueCounts = result.RootElement.GetProperty("TokenIssueCounts");
        var accessTokensByType = tokenIssueCounts.GetProperty("AccessTokensByType");
        accessTokensByType.GetProperty("Jwt").GetInt64().ShouldBe(0);
        accessTokensByType.GetProperty("Reference").GetInt64().ShouldBe(0);
        var senderConstraint = tokenIssueCounts.GetProperty("AccessTokensBySenderConstraint");
        senderConstraint.GetProperty("None").GetInt64().ShouldBe(0);
        senderConstraint.GetProperty("DPoP").GetInt64().ShouldBe(0);
        senderConstraint.GetProperty("mTLS").GetInt64().ShouldBe(0);
        var tokensByType = tokenIssueCounts.GetProperty("TokensByType");
        tokensByType.GetProperty("Access").GetInt64().ShouldBe(0);
        tokensByType.GetProperty("Refresh").GetInt64().ShouldBe(0);
        tokensByType.GetProperty("Id").GetInt64().ShouldBe(0);
    }

    private void IssueToken(string grantType, bool accessTokenIssued, AccessTokenType? accessTokenType, bool refreshTokenIssued,
        ProofType proofType, bool idTokenIssued) =>
        Duende.IdentityServer.Telemetry.Metrics.TokenIssued("ClientId", grantType, null, accessTokenIssued, accessTokenType, refreshTokenIssued, proofType, idTokenIssued);
}
