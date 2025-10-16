// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Documentation.Mcp.Server.Database;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReverseMarkdown;
using SimpleFeedReader;

namespace Duende.Documentation.Mcp.Server.Sources.Blog;

public class BlogArticleIndexer(IServiceProvider services, ILogger<BlogArticleIndexer> logger) : BackgroundService
{
    private readonly TimeSpan _maxAge = TimeSpan.FromDays(2);
    private static readonly DateTime ReferenceDate = new(2024, 10, 01);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunIndexerAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }

    private async Task RunIndexerAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Running blog indexer");

        // Scope
        using var scope = services.CreateScope();
        await using var db = scope.ServiceProvider.GetRequiredService<McpDb>();
        using var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();

        // Check if we need to update
        var lastUpdate = await db.GetLastUpdateStateAsync("blog");
        if (lastUpdate > DateTimeOffset.UtcNow.Add(-_maxAge))
        {
            logger.LogInformation("Skipping blog indexer, last update was {lastUpdate}", lastUpdate);
            return;
        }

        // Fetch RSS
        var reader = new FeedReader();
        var items = await reader.RetrieveFeedAsync("https://duendesoftware.com/rss.xml", stoppingToken);
        var filteredItems = items
            .Where(it => it.PublishDate >= ReferenceDate && it.Categories?.Contains("blog") == true).ToList();

        await db.FTSBlogArticle.ExecuteDeleteAsync(stoppingToken);

        foreach (var filteredItem in filteredItems)
        {
            await RunIndexerForDocumentAsync(filteredItem.Title ?? "", filteredItem.GetSummary(), filteredItem.Uri, db, httpClient, stoppingToken);
        }

        await db.SaveChangesAsync(stoppingToken);
        logger.LogInformation("Saved {count} blog articles", db.FTSBlogArticle.Count());

        await db.SetLastUpdateStateAsync("blog", DateTimeOffset.UtcNow);

        logger.LogInformation("Finished blog indexer");
    }

    private async Task RunIndexerForDocumentAsync(string title, string? description, Uri? url, McpDb db, HttpClient httpClient, CancellationToken stoppingToken)
    {
        if (url == null)
        {
            return;
        }

        // Start indexing
        var htmlContent = await httpClient.GetStringAsync(url, stoppingToken);

        // Parse HTML and find content
        var htmlDocument = new HtmlDocument();
        htmlDocument.LoadHtml(htmlContent);
        var content = htmlDocument.DocumentNode.SelectSingleNode("//section[@class='page-content alt markdown']");

        // Convert to Markdown
        var markdownContent = new Converter(new Config
        {
            GithubFlavored = true
        }).Convert(content!.InnerHtml); ;

        db.FTSBlogArticle.Add(new FTSBlogArticle
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Content = !string.IsNullOrEmpty(description)
                ? $"Summary: {description}\n\n---\n\n{markdownContent}"
                : markdownContent
        });
    }
}
