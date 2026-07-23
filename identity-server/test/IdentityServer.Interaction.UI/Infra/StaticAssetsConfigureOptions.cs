// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.UI.Infra;

internal class StaticAssetsConfigureOptions(string prefix, IWebHostEnvironment environment) : IPostConfigureOptions<StaticFileOptions>
{
    public void PostConfigure(string? name, StaticFileOptions options)
    {
        if (options.FileProvider == null && environment.WebRootFileProvider == null)
        {
            throw new InvalidOperationException("Missing FileProvider.");
        }
        options.FileProvider = environment.WebRootFileProvider;

        var assembly = GetType().Assembly;
        var filesProvider = new ManifestEmbeddedFileProvider(assembly, prefix + "/wwwroot");
        options.FileProvider = new CompositeFileProvider(options.FileProvider, filesProvider);
    }
}
