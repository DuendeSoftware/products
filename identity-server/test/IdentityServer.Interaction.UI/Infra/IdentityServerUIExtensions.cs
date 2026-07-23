// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;

namespace Duende.IdentityServer.UI.Infra;

public static class IdentityServerUIExtensions
{
    public static void ServeEmbeddedUi(this WebApplicationBuilder builder, string prefix) =>
        builder.Services.ConfigureOptions(new StaticAssetsConfigureOptions(prefix, builder.Environment));
}
