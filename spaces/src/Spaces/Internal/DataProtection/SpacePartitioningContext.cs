// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Spaces.Internal.Telemetry;
using Microsoft.Extensions.Logging;

namespace Duende.Spaces.Internal.DataProtection;

internal sealed record SpacePartitioningContext(SpacesMetrics Metrics, ILogger Logger);
