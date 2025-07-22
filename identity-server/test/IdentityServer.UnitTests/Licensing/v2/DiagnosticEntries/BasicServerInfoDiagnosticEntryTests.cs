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
        var expectedServerStartTime = DateTime.UtcNow.AddMinutes(-5);
        var expectedCurrentServerTime = DateTime.UtcNow;
        var subject = new BasicServerInfoDiagnosticEntry(() => expectedHostName);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject, expectedServerStartTime, expectedCurrentServerTime);

        var basicServerInfo = result.RootElement.GetProperty("BasicServerInfo");
        basicServerInfo.GetProperty("HostName").GetString().ShouldBe(expectedHostName);
        basicServerInfo.GetProperty("ServerStartTime").GetString().ShouldBe(expectedServerStartTime.ToString("o"));
        basicServerInfo.GetProperty("CurrentServerTime").GetString().ShouldBe(expectedCurrentServerTime.ToString("o"));
    }
}
