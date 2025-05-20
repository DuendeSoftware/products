// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Microsoft.AspNetCore.Authentication;
using UnitTests.Common;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class AuthSchemeInfoDiagnosticEntryTests
{

    private readonly MockAuthenticationSchemeProvider _mockAuthenticationSchemeProvider;
    private readonly AuthSchemeInfoDiagnosticEntry _subject;

    public AuthSchemeInfoDiagnosticEntryTests()
    {
        _mockAuthenticationSchemeProvider = new MockAuthenticationSchemeProvider();
        _subject = new AuthSchemeInfoDiagnosticEntry(_mockAuthenticationSchemeProvider);
    }

    [Fact]
    public async Task WriteAsync_ShouldWriteAuthSchemeInfo()
    {
        var testAuthenticationScheme = new AuthenticationScheme("TestScheme", "Test Scheme", typeof(MockAuthenticationHandler));
        _mockAuthenticationSchemeProvider.RemoveScheme("scheme");
        _mockAuthenticationSchemeProvider.AddScheme(testAuthenticationScheme);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var authSchemeInfo = result.RootElement.GetProperty("AuthSchemeInfo");
        var authSchemes = authSchemeInfo.GetProperty("Schemes");
        var firstEntry = authSchemes.EnumerateArray().First();
        firstEntry.GetProperty("TestScheme").GetString().ShouldBe("UnitTests.Common.MockAuthenticationHandler");
    }

    [Fact]
    public async Task WriteAsync_ShouldWriteAllRegisteredAuthSchemes()
    {
        _mockAuthenticationSchemeProvider.RemoveScheme("scheme");
        _mockAuthenticationSchemeProvider.AddScheme(new AuthenticationScheme("FirstTestScheme", "First Test Scheme", typeof(MockAuthenticationHandler)));
        _mockAuthenticationSchemeProvider.AddScheme(new AuthenticationScheme("SecondTestScheme", "Second Test Scheme", typeof(MockAuthenticationHandler)));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(_subject);

        var authSchemeInfo = result.RootElement.GetProperty("AuthSchemeInfo");
        var authSchemes = authSchemeInfo.GetProperty("Schemes");
        authSchemes.GetArrayLength().ShouldBe(2);
    }
}
