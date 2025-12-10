// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class TestDPoPNonceValidator
{
    public TestDPoPNonceValidator(
        IOptionsMonitor<DPoPOptions> optionsMonitor) => _internalValidator = new(
            optionsMonitor,
            new EphemeralDataProtectionProvider(),
            new FakeTimeProvider(),
            new NullLogger<DefaultDPoPNonceValidator>());

    internal DefaultDPoPNonceValidator _internalValidator;

    public IDataProtector TestDataProtector => _internalValidator.DataProtector;
    public FakeTimeProvider TestTimeProvider => (FakeTimeProvider)_internalValidator.TimeProvider;

    public NonceValidationResult ValidateNonce(DPoPProofValidationContext context, string? nonce)
        => _internalValidator.ValidateNonce(context, nonce);

    public string CreateNonce(DPoPProofValidationContext context)
        => _internalValidator.CreateNonce(context);

    public long GetUnixTimeFromNonce(string nonce)
        => _internalValidator.GetUnixTimeFromNonce(nonce);

    public bool IsExpired(DPoPProofValidationContext context, long time)
        => _internalValidator.IsExpired(context, time);
}
