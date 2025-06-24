// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.Configuration;
using Microsoft.Extensions.Configuration;

namespace Duende.Bff.Builder;

public interface IBffServicesBuilder : IBffBuilder
{
    internal void RegisterConfigurationLoader(LoadPluginConfiguration loadPluginConfiguration);
    public IBffServicesBuilder LoadConfiguration(IConfiguration section);
}
