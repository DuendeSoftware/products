// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Duende.IdentityServer.UI;

internal class StaticAssetsConfigureOptions(IWebHostEnvironment environment) : IPostConfigureOptions<StaticFileOptions>
{
    public void PostConfigure(string? name, StaticFileOptions options)
    {
        options.ContentTypeProvider = options.ContentTypeProvider ?? new FileExtensionContentTypeProvider();
        if (options.FileProvider == null && environment.WebRootFileProvider == null)
        {
            throw new InvalidOperationException("Missing FileProvider.");
        }
        options.FileProvider = options.FileProvider ?? environment.WebRootFileProvider;

        var assembly = GetType().Assembly;
        var filesProvider = new ManifestEmbeddedFileProvider(assembly, "wwwroot");
        options.FileProvider = new CompositeFileProvider(options.FileProvider, filesProvider);
    }
}

public static class IdentityServerUIExtensions
{
    public static void AddIdentityServerUI(this WebApplicationBuilder builder) => builder.Services.ConfigureOptions<StaticAssetsConfigureOptions>();
}
