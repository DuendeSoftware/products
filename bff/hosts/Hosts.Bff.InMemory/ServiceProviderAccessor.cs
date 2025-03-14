// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Bff;

/// <summary>
/// A workaround to get the service provider available in the ConfigureServices method
/// </summary>
internal class ServiceProviderAccessor
{
    public IServiceProvider? ServiceProvider { get; set; }
}
