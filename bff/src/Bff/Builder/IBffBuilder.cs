// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Builder;

public interface IBffBuilder
{
    IServiceCollection Services { get; }
}
