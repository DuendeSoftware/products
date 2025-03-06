// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DPoPApi;

public class DPoPOptions
{
    public DPoPMode Mode { get; set; } = DPoPMode.DPoPOnly;

    public TimeSpan ProofTokenValidityDuration { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan ClientClockSkew { get; set; } = TimeSpan.FromMinutes(0);
    public TimeSpan ServerClockSkew { get; set; } = TimeSpan.FromMinutes(5);

    public bool ValidateIat { get; set; } = true;
    public bool ValidateNonce { get; set; } = false;
}
