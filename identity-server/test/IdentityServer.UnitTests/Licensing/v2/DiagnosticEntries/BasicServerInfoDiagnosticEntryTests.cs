// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

namespace IdentityServer.UnitTests.Licensing.v2.DiagnosticEntries;

public class BasicServerInfoDiagnosticEntryTests
{
    [Fact]
    public async Task WriteAsync_ShouldWriteBasicServerInfo()
    {
        const string expectedHostName = "testing.local";
        var subject = new BasicServerInfoDiagnosticEntry(() => expectedHostName);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var basicServerInfo = result.RootElement.GetProperty("BasicServerInfo");
        basicServerInfo.GetProperty("HostName").GetString().ShouldBe(expectedHostName);
    }
}
