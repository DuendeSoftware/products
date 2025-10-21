// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.IO.Compression;
using System.Text;
using Documentation.Mcp.Database;
using Documentation.Mcp.Infrastructure;
using Documentation.Mcp.Sources.Docs;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Documentation.Mcp.Sources.Samples;

public class SamplesIndexer(IServiceProvider services, ILogger<DocsArticleIndexer> logger) : BackgroundService
{
    private readonly TimeSpan _maxAge = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunIndexerAsync(stoppingToken);
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
            logger.LogInformation("Skipping samples indexer, last update was {lastUpdate}", lastUpdate);
            return;
        }

        // Explore llms.txt specific to samples
        var llmsTxt = await httpClient.GetStringAsync("https://docs.duendesoftware.com/_llms-txt/identityserver-sample-code.txt", stoppingToken);
        llmsTxt = llmsTxt.Replace("###", "\n\n###"); // keep sample titles on separate lines (rehype in docs does minification)
        llmsTxt = llmsTxt.Replace("* ", "\n* "); // keep lists on separate lines (rehype in docs does minification)
        var llmsMd = Markdig.Markdown.Parse(llmsTxt);

        // Download samples repository blob
        await using var samplesRepositoryBlobStream = await httpClient.GetStreamAsync("https://github.com/duendesoftware/samples/archive/refs/heads/main.zip", stoppingToken);
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
                    var files = await GetFilesForRepositoryPathAsync(sampleContent.ToString(), samplesRepositoryZipStream);
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
            var files = await GetFilesForRepositoryPathAsync(sampleContent.ToString(), samplesRepositoryZipStream);
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
        logger.LogInformation("Saved {count} samples", db.FTSSampleProject.Count());

        await db.SetLastUpdateStateAsync("samples", DateTimeOffset.UtcNow);

        logger.LogInformation("Finished samples indexer");
    }

    private string ExtractTitle(string markdownText)
    {
        // Remove:
        // [Section titled “.......”](#custom-profile-service)
        var indexOfSection = markdownText.IndexOf("[Section", StringComparison.OrdinalIgnoreCase);
        if (indexOfSection > 0)
        {
            markdownText = markdownText.Substring(0, indexOfSection - 1);
        }

        return markdownText;
    }

    private async Task<List<string>> GetFilesForRepositoryPathAsync(string markdownText, ZipArchive repositoryArchive)
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

                var sampleRootPath = "samples-main" + link.Url!.Substring(sampleRootIndex);
                var sampleEntries = repositoryArchive.Entries
                    .Where(e =>

                        e.FullName.StartsWith(sampleRootPath, StringComparison.OrdinalIgnoreCase) &&

                        // Only C# files
                        (e.FullName.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                         e.FullName.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ||

                         // Special case for passkeys sample
                         (e.FullName.Contains("passkey", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                    ));

                foreach (var sampleEntry in sampleEntries)
                {
                    using var sampleEntryStream = new StreamReader(sampleEntry.Open());
                    var sampleContents = await sampleEntryStream.ReadToEndAsync();

                    files.Add("File: `" + sampleEntry.FullName + "`:\n```\n" + sampleContents + "\n```");
                }
            }
        }

        return files;
    }
}
