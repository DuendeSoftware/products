// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal.Telemetry;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Duende.Spaces.Internal.DataProtection;

internal sealed class SpacePartitionedDataProtectionProvider(
    IDataProtectionProvider root,
    ISpaceContextAccessor accessor,
    SpacesMetrics metrics,
    ILogger logger) : IDataProtectionProvider, IUnpartitionedDataProtectionSource
{
    private readonly SpacePartitioningContext _context = new(metrics, logger);

    public IDataProtector CreateProtector(string purpose) =>
        new SpacePartitionedDataProtector(root, accessor, [purpose], _context);

    public IDataProtectionProvider GetUnpartitioned() => root;
}
