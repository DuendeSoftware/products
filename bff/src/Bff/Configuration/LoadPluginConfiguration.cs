// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Bff.Configuration;

/// <summary>
/// Delegate for loading plugin configuration.This is needed because the actual configuration contains information
/// for both the BFF and it's plugins. (In effect, the plugin data is merged into the same IConfiguration object).
///
/// Each plugin can also provide its own configuration loader, which is invoked when the BFF is configured.
/// </summary>
/// <param name="services"></param>
/// <param name="configuration"></param>
internal delegate void LoadPluginConfiguration(IServiceCollection services, IConfiguration configuration);
