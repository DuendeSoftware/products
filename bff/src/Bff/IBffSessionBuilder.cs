// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff;

public interface IBffSessionBuilder : IBffPartBuilder
{

    internal IServiceCollection InternalServices => BffApplicationBuilder.InternalServices;

}
