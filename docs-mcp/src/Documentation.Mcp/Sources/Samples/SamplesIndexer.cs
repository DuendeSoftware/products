// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Text;
using Documentation.Mcp.Database;
using Documentation.Mcp.Infrastructure;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Documentation.Mcp.Sources.Samples;

internal sealed class SamplesIndexer(IServiceProvider services, ILogger<SamplesIndexer> logger) : BackgroundService
{
    private readonly TimeSpan _maxAge = TimeSpan.FromDays(7);

    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIndexerAsync(stoppingToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Error running samples indexer. Try restarting the application.");
            }
            await Task.Delay(TimeSpan.FromHours(8), stoppingToken);
        }
    }

    private async Task RunIndexerAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Running samples indexer");

        // Scope
        using var scope = services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<McpDb>();
        using var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();

        // Check if we need to update
        var lastUpdate = await db.GetLastUpdateStateAsync("samples");
        if (lastUpdate > DateTimeOffset.UtcNow.Add(-_maxAge))
        {
            logger.LogInformation("Skipping samples indexer, last update was {LastUpdate}", lastUpdate);
            return;
        }

        // Explore llms.txt specific to samples
        var llmsUri = new Uri("https://docs.duendesoftware.com/_llms-txt/identityserver-sample-code.txt");
        var llmsTxt = await httpClient.GetStringAsync(llmsUri, stoppingToken);
        llmsTxt = llmsTxt.Replace("###", "\n\n###", StringComparison.OrdinalIgnoreCase); // keep sample titles on separate lines (rehype in docs does minification)
        llmsTxt = llmsTxt.Replace("* ", "\n* ", StringComparison.OrdinalIgnoreCase); // keep lists on separate lines (rehype in docs does minification)
        var llmsMd = Markdig.Markdown.Parse(llmsTxt);

        // Download samples repository blob
        var samplesBlobUri = new Uri("https://github.com/duendesoftware/samples/archive/refs/heads/main.zip");
        await using var samplesRepositoryBlobStream = await httpClient.GetStreamAsync(samplesBlobUri, stoppingToken);
        await using var samplesRepositoryTempStream = await TemporaryFileStream.CreateFromAsync(samplesRepositoryBlobStream);
        await using var samplesRepositoryZipStream = new ZipArchive(samplesRepositoryTempStream, ZipArchiveMode.Read, leaveOpen: false);

        await db.FTSSampleProject.ExecuteDeleteAsync(stoppingToken);

        // Extract sample titles and descriptions
        string? sampleTitle = null;
        StringBuilder? sampleContent = null;
        foreach (var block in llmsMd)
        {
            // New sample
            if (block is HeadingBlock headingBlock)
            {
                if (sampleTitle == null)
                {
                    sampleTitle = llmsTxt.Substring(headingBlock.Span.Start, headingBlock.Span.Length).TrimStart('#', ' ');
                    sampleContent = new StringBuilder();
                }
                else if (sampleContent != null)
                {
                    var files = await GetFilesForRepositoryPathAsync(sampleContent.ToString(), samplesRepositoryZipStream, stoppingToken);
                    if (files.Count > 0)
                    {
                        db.FTSSampleProject.Add(new FTSSampleProject
                        {
                            Id = Guid.NewGuid().ToString(),
                            Product = "IdentityServer",
                            Title = ExtractTitle(sampleTitle),
                            Description = sampleContent.ToString(),
                            Files = files
                        });
                    }

                    sampleTitle = llmsTxt.Substring(headingBlock.Span.Start, headingBlock.Span.Length).TrimStart('#', ' ');
                    sampleContent.Clear();
                }

                // HACK: Sprinkle some keywords
                if (sampleTitle.Contains("passkey", StringComparison.OrdinalIgnoreCase))
                {
                    sampleTitle += " webauthn fido2 yubikey passwordless";
                }
            }

            if (sampleTitle != null && sampleContent != null)
            {
                sampleContent.AppendLine(llmsTxt.Substring(block.Span.Start, block.Span.Length));
            }
        }

        if (sampleTitle != null && sampleContent != null)
        {
            var files = await GetFilesForRepositoryPathAsync(sampleContent.ToString(), samplesRepositoryZipStream, stoppingToken);
            if (files.Count > 0)
            {
                db.FTSSampleProject.Add(new FTSSampleProject
                {
                    Id = Guid.NewGuid().ToString(),
                    Product = "IdentityServer",
                    Title = ExtractTitle(sampleTitle),
                    Description = sampleContent.ToString(),
                    Files = files
                });
            }
        }

        await db.SaveChangesAsync(stoppingToken);
        logger.LogInformation("Saved {Count} samples", db.FTSSampleProject.Count());

        await db.SetLastUpdateStateAsync("samples", DateTimeOffset.UtcNow);

        logger.LogInformation("Finished samples indexer");
    }

    private static string ExtractTitle(string markdownText)
    {
        // Remove:
        // [Section titled “.......”](#custom-profile-service)
        var indexOfSection = markdownText.IndexOf("[Section", StringComparison.OrdinalIgnoreCase);
        if (indexOfSection > 0)
        {
            markdownText = markdownText[..(indexOfSection - 1)];
        }

        return markdownText;
    }

    private static async Task<List<string>> GetFilesForRepositoryPathAsync(
        string markdownText,
        ZipArchive repositoryArchive,
        CancellationToken cancellationToken)
    {
        var files = new List<string>();

        var markdown = Markdig.Markdown.Parse(markdownText);
        foreach (var link in markdown.Descendants<LinkInline>())
        {
            // MD: https://github.com/DuendeSoftware/samples/tree/main/IdentityServer/v7/AspNetIdentityPasskeys
            // ZIP: samples-main/IdentityServer/v6/UserInteraction/StepUp/IdentityServerHost/Pages/ExternalLogin/Callback.cshtml.cs
            if (link.Url?.Contains("github.com/duendesoftware/samples/", StringComparison.OrdinalIgnoreCase) == true)
            {
                var sampleRootIndex = link.Url!.IndexOf("/IdentityServer/v7/", StringComparison.OrdinalIgnoreCase);
                if (sampleRootIndex < 0)
                {
                    continue;
                }

                var sampleRootPath = $"samples-main{link.Url![sampleRootIndex..]}";
                const string sharedHostRootPath = "samples-main/IdentityServer/v7/IdentityServerHost";
                var sampleEntries = repositoryArchive.Entries
                    .Where(e =>

                        (e.FullName.StartsWith(sampleRootPath, StringComparison.OrdinalIgnoreCase) ||
                         e.FullName.StartsWith(sharedHostRootPath, StringComparison.OrdinalIgnoreCase)) &&

                        // Only C# files
                        (e.FullName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                         e.FullName.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ||

                         // Special case for passkeys sample
                         (e.FullName.Contains("passkey", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                    ));

                foreach (var sampleEntry in sampleEntries)
                {
                    using var sampleEntryStream = new StreamReader(await sampleEntry.OpenAsync(cancellationToken));
                    var sampleContents = await sampleEntryStream.ReadToEndAsync(cancellationToken);

                    files.Add("File: `" + sampleEntry.FullName + "`:\n```\n" + sampleContents + "\n```");
                }
            }
        }

        return files;
    }
}
