// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using Duende.Private.Licensing;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Licensing.V2;

internal class LicenseAccessor(
    GetLicenseKey getLicenseKey,
    ILogger<LicenseAccessor<Private.Licensing.IdentityServerLicense>> logger)
    : LicenseAccessor<Duende.Private.Licensing.IdentityServerLicense>(getLicenseKey, logger);

