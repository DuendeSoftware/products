// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Builder;

internal sealed class BffSessionBuilder(IBffApplicationBuilder applicationBuilder) : IBffSessionBuilder
{
    public IBffApplicationBuilder BffApplicationBuilder { get; } = applicationBuilder;
}
