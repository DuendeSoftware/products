// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Xml.Linq;
using Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;
using Microsoft.Extensions.Options;

namespace IdentityServer.UnitTests.Licensing.V2.DiagnosticEntries;

public class DataProtectionDiagnosticEntryTests
{
    [Fact]
    public async Task WriteAsync_WithConfiguredOptions_ShouldWriteCorrectValues()
    {
        var dataProtectionOptions = Options.Create(new DataProtectionOptions
        {
            ApplicationDiscriminator = "TestApplication"
        });
        var keyManagementOptions = Options.Create(new KeyManagementOptions
        {
            XmlEncryptor = new TestXmlEncryptor(),
            XmlRepository = new TestXmlRepository()
        });
        var subject = new DataProtectionDiagnosticEntry(dataProtectionOptions, keyManagementOptions);

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var dataProtectionConfiguration = result.RootElement.GetProperty("DataProtectionConfiguration");
        dataProtectionConfiguration.GetProperty("ApplicationDiscriminator").GetString().ShouldBe("TestApplication");
        dataProtectionConfiguration.GetProperty("XmlEncryptor").GetString().ShouldBe(typeof(TestXmlEncryptor).FullName);
        dataProtectionConfiguration.GetProperty("XmlRepository").GetString().ShouldBe(typeof(TestXmlRepository).FullName);
    }

    [Fact]
    public async Task WriteAsync_WithDefaultOptions_ShouldWriteDefaultValues()
    {
        var subject = new DataProtectionDiagnosticEntry(
            Options.Create(new DataProtectionOptions()),
            Options.Create(new KeyManagementOptions()));

        var result = await DiagnosticEntryTestHelper.WriteEntryToJson(subject);

        var dataProtectionConfiguration = result.RootElement.GetProperty("DataProtectionConfiguration");
        dataProtectionConfiguration.GetProperty("ApplicationDiscriminator").GetString().ShouldBe("Not Configured");
        dataProtectionConfiguration.GetProperty("XmlEncryptor").GetString().ShouldBe("Not Configured");
        dataProtectionConfiguration.GetProperty("XmlRepository").GetString().ShouldBe("Not Configured");
    }

    private class TestXmlEncryptor : IXmlEncryptor
    {
        public EncryptedXmlInfo Encrypt(XElement plaintextElement) => new EncryptedXmlInfo(plaintextElement, typeof(TestXmlEncryptor));
    }

    private class TestXmlRepository : IXmlRepository
    {
        public IReadOnlyCollection<XElement> GetAllElements() => Array.Empty<XElement>();

        public void StoreElement(XElement element, string friendlyName)
        {
        }
    }
}
