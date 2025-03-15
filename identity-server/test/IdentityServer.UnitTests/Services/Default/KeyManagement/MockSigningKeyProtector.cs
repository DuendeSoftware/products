// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services.KeyManagement;
using Microsoft.AspNetCore.DataProtection;

namespace UnitTests.Services.Default.KeyManagement;

internal class MockSigningKeyProtector : ISigningKeyProtector
{
    private IDataProtector _dataProtector;
    public bool ProtectWasCalled { get; set; }

    public MockSigningKeyProtector()
    {
        var provider = new EphemeralDataProtectionProvider();
        _dataProtector = provider.CreateProtector("test");
    }

    public SerializedKey Protect(KeyContainer key)
    {
        ProtectWasCalled = true;
        return new SerializedKey
        {
            Id = key.Id,
            Algorithm = key.Algorithm,
            IsX509Certificate = key.HasX509Certificate,
            Created = key.Created,
            Data = _dataProtector.Protect(KeySerializer.Serialize(key)),
        };
    }

    /// <summary>
    /// Simulate a situation where a signing key was protected in the past with a signing key that is no longer available
    /// </summary>
    public SerializedKey ProtectAndLoseDataProtectionKey(KeyContainer key)
    {
        var provider = new EphemeralDataProtectionProvider();
        var badProtector = provider.CreateProtector("unavailable-when-we-unprotect");

        ProtectWasCalled = true;
        return new SerializedKey
        {
            Id = key.Id,
            Algorithm = key.Algorithm,
            IsX509Certificate = key.HasX509Certificate,
            Created = key.Created,
            Data = badProtector.Protect(KeySerializer.Serialize(key)),
        };
    }

    public KeyContainer Unprotect(SerializedKey key)
    {
        return KeySerializer.Deserialize<RsaKeyContainer>(_dataProtector.Unprotect(key.Data));
    }
}
