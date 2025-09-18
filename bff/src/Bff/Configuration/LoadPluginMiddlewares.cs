// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;

namespace Duende.Bff.Configuration;

/// <summary>
/// Delegate for setting up middleware loaders so plugins can add their own middlewares to the BFF pipeline.
/// </summary>
/// <param name="appBuilder"></param>
internal delegate void LoadPluginMiddlewares(IApplicationBuilder appBuilder);
