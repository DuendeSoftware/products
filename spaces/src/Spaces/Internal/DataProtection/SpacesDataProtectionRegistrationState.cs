// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Spaces.Internal.DataProtection;

internal sealed class SpacesDataProtectionRegistrationState(IServiceCollection services, ServiceDescriptor installed)
{
    public ServiceDescriptor Installed { get; } = installed;

    public IReadOnlyList<ServiceDescriptor> NonKeyedProviderDescriptors() =>
        [.. services.Where(d => d.ServiceType == typeof(IDataProtectionProvider) && !d.IsKeyedService)];
}
