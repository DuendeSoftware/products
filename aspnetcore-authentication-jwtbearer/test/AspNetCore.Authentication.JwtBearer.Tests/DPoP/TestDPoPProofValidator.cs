// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class TestDPoPProofValidator
{
    public TestDPoPProofValidator(
        IOptionsMonitor<DPoPOptions> optionsMonitor,
        IReplayCache replayCache) => _internalValidator = new(
            optionsMonitor,
            new EphemeralDataProtectionProvider(),
            replayCache,
            new FakeTimeProvider(),
            new NullLogger<DPoPProofValidator>());

    internal DPoPProofValidator _internalValidator;

    public IDataProtector TestDataProtector => _internalValidator.DataProtector;
    public FakeTimeProvider TestTimeProvider => (FakeTimeProvider)_internalValidator.TimeProvider;
    public IReplayCache TestReplayCache => _internalValidator.ReplayCache;

    public void ValidatePayload(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => _internalValidator.ValidatePayload(context, result);

    public Task ValidateReplay(DPoPProofValidationContext context, DPoPProofValidationResult result, CancellationToken cancellationToken = default)
        => _internalValidator.ValidateReplay(context, result, cancellationToken);

    public void ValidateFreshness(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => _internalValidator.ValidateFreshness(context, result);

    public void ValidateIat(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => _internalValidator.ValidateIat(context, result);

    public void ValidateNonce(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => _internalValidator.ValidateNonce(context, result);

    public string CreateNonce(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => _internalValidator.CreateNonce(context, result);

    public long GetUnixTimeFromNonce(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => _internalValidator.GetUnixTimeFromNonce(context, result);

    public bool IsExpired(TimeSpan validityDuration, TimeSpan clockSkew, long issuedAtTime)
        => _internalValidator.IsExpired(validityDuration, clockSkew, issuedAtTime);

    public bool IsExpired(DPoPProofValidationContext context, DPoPProofValidationResult result, long time,
        ExpirationValidationMode mode) =>
        _internalValidator.IsExpired(context, result, time, mode);

    public void ValidateCnf(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => _internalValidator.ValidateCnf(context, result);

    public async Task ValidateToken(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => await _internalValidator.ValidateToken(context, result);

    public void ValidateJwk(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => _internalValidator.ValidateJwk(context, result);

}
