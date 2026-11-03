// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Private.Licencing.V2;

namespace Duende.Bff.Licensing;

internal sealed class BffLicenseValidator(LicenseValidator validator)
{
    internal void ValidateLicense() => validator.ValidateLicenseExpiry();
}
