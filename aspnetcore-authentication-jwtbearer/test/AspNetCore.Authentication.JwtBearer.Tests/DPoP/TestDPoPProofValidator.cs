// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

public class TestDPoPProofValidator : DefaultDPoPProofValidator
{
    public TestDPoPProofValidator(
        IOptionsMonitor<DPoPOptions> optionsMonitor,
        IReplayCache replayCache) : base(
            optionsMonitor,
            new EphemeralDataProtectionProvider(),
            replayCache,
            new FakeTimeProvider(),
            Substitute.For<ILogger<DefaultDPoPProofValidator>>())
    { }

    public IDataProtector TestDataProtector => DataProtector;
    public FakeTimeProvider TestTimeProvider => (FakeTimeProvider)TimeProvider;
    public IReplayCache TestReplayCache => ReplayCache;

    public new Task ValidateHeader(DPoPProofValidationContext context, DPoPProofValidationResult result, CancellationToken cancellationToken = default) => base.ValidateHeader(context, result, cancellationToken);

    public new Task ValidatePayload(DPoPProofValidationContext context, DPoPProofValidationResult result, CancellationToken cancellationToken = default)
        => base.ValidatePayload(context, result, cancellationToken);

    public new Task ValidateReplay(DPoPProofValidationContext context, DPoPProofValidationResult result, CancellationToken cancellationToken = default)
        => base.ValidateReplay(context, result, cancellationToken);

    public new Task ValidateFreshness(DPoPProofValidationContext context, DPoPProofValidationResult result, CancellationToken cancellationToken = default)
        => base.ValidateFreshness(context, result, cancellationToken);

    public new Task ValidateIat(DPoPProofValidationContext context, DPoPProofValidationResult result, CancellationToken cancellationToken = default)
        => base.ValidateIat(context, result, cancellationToken);

    public new Task ValidateNonce(DPoPProofValidationContext context, DPoPProofValidationResult result, CancellationToken cancellationToken = default)
        => base.ValidateNonce(context, result, cancellationToken);

    public new string CreateNonce(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => base.CreateNonce(context, result);

    public new ValueTask<long> GetUnixTimeFromNonceAsync(DPoPProofValidationContext context, DPoPProofValidationResult result)
        => base.GetUnixTimeFromNonceAsync(context, result);

    public new virtual bool IsExpired(TimeSpan validityDuration, TimeSpan clockSkew, long issuedAtTime)
        => base.IsExpired(validityDuration, clockSkew, issuedAtTime);

    public new virtual bool IsExpired(DPoPProofValidationContext context, DPoPProofValidationResult result, long time,
        ExpirationValidationMode mode) =>
        base.IsExpired(context, result, time, mode);
}
