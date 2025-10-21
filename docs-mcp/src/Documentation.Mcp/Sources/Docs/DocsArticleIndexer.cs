// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text;
using Documentation.Mcp.Database;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Documentation.Mcp.Sources.Docs;

internal class DocsArticleIndexer(IServiceProvider services, ILogger<DocsArticleIndexer> logger) : BackgroundService
{
    private readonly TimeSpan _maxAge = TimeSpan.FromDays(2);

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
        logger.LogInformation("Running docs indexer");

        // Scope
        using var scope = services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<McpDb>();
        using var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();

        // Check if we need to update
        var lastUpdate = await db.GetLastUpdateStateAsync("docs");
        if (lastUpdate > DateTimeOffset.UtcNow.Add(-_maxAge))
        {
            logger.LogInformation("Skipping docs indexer, last update was {LastUpdate}", lastUpdate);
            return;
        }

        // Explore llms.txt
        var llmsUrl = new Uri("https://docs.duendesoftware.com/llms.txt");
        var llmsTxt = await httpClient.GetStringAsync(llmsUrl, stoppingToken);
        var llmsMd = Markdig.Markdown.Parse(llmsTxt);

        await db.FTSDocsArticle.ExecuteDeleteAsync(stoppingToken);

        foreach (var link in llmsMd.Descendants<LinkInline>())
        {
            if (link.Url?.Contains("_llms-txt/", StringComparison.OrdinalIgnoreCase) == true)
            {
                var title = link.Title ?? link.FirstChild?.ToString() ?? "Unknown";
                var description = link.NextSibling is LiteralInline literal ? literal.Content.Text.TrimStart(':', ' ') : "";

                await RunIndexerForDocument(title, description, link.Url!, db, httpClient, stoppingToken);
            }
        }

        await db.SaveChangesAsync(stoppingToken);
        logger.LogInformation("Saved {Count} docs articles", db.FTSDocsArticle.Count());

        await db.SetLastUpdateStateAsync("docs", DateTimeOffset.UtcNow);

        logger.LogInformation("Finished docs indexer");
    }

    private static async Task RunIndexerForDocument(
        string title,
        string description,
        string linkUrl,
        McpDb db,
        HttpClient httpClient,
        CancellationToken stoppingToken)
    {
        // Start indexing
        var llmsTxt = await httpClient.GetStringAsync(linkUrl, stoppingToken);
        var llmsMd = Markdig.Markdown.Parse(llmsTxt);

        string? articleTitle = null;
        StringBuilder? articleContent = null;
        foreach (var block in llmsMd)
        {
            if (block is HeadingBlock { Level: 1 } h1)
            {
                if (articleTitle == null)
                {
                    articleTitle = llmsTxt.Substring(h1.Span.Start, h1.Span.Length).TrimStart('#', ' ');
                    articleContent = new StringBuilder();
                }
                else if (articleContent != null)
                {
                    db.FTSDocsArticle.Add(new FTSDocsArticle
                    {
                        Id = Guid.NewGuid().ToString(),
                        Product = title,
                        Title = articleTitle,
                        Content = articleContent.ToString(),
                    });

                    articleTitle = llmsTxt.Substring(h1.Span.Start, h1.Span.Length).TrimStart('#', ' ');
                    articleContent.Clear();
                }
            }

            if (articleContent != null)
            {
                articleContent.AppendLine(llmsTxt.Substring(block.Span.Start, block.Span.Length));
            }
        }

        if (articleTitle != null && articleContent != null)
        {
            db.FTSDocsArticle.Add(new FTSDocsArticle
            {
                Id = Guid.NewGuid().ToString(),
                Product = title,
                Title = articleTitle,
                Content = articleContent.ToString(),
            });
        }
    }
}
