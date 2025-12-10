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
        IReplayCache replayCache)
    {
        _nonceValidator = new TestDPoPNonceValidator(optionsMonitor);
        _internalValidator = new(
            optionsMonitor,
            _nonceValidator._internalValidator,
            replayCache,
            _nonceValidator.TestTimeProvider,
            new NullLogger<DPoPProofValidator>());
    }

    internal DPoPProofValidator _internalValidator;
    internal TestDPoPNonceValidator _nonceValidator;

    public IDataProtector TestDataProtector => _nonceValidator.TestDataProtector;
    public FakeTimeProvider TestTimeProvider => _nonceValidator.TestTimeProvider;
    public IReplayCache TestReplayCache => _internalValidator.ReplayCache;

    public void ValidatePayload(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => _internalValidator.ValidatePayload(context, result);

    public Task ValidateReplay(DPoPProofValidationContext context, DPoPProofValidationResult result, CancellationToken cancellationToken = default)
        => _internalValidator.ValidateReplay(context, result, cancellationToken);

    public void ValidateFreshness(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => _internalValidator.ValidateFreshness(context, result);

    public void ValidateIat(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => _internalValidator.ValidateIat(context, result);

    public NonceValidationResult ValidateNonce(DPoPProofValidationContext context, string? nonce)
        => _nonceValidator.ValidateNonce(context, nonce);

    public string CreateNonce(DPoPProofValidationContext context)
        => _nonceValidator.CreateNonce(context);

    public long GetUnixTimeFromNonce(string nonce)
        => _nonceValidator.GetUnixTimeFromNonce(nonce);

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
