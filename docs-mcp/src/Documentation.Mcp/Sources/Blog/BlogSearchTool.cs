// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Globalization;
using System.Text;
using Documentation.Mcp.Database;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Documentation.Mcp.Sources.Blog;

[McpServerToolType]
internal sealed class BlogSearchTool(McpDb db)
{
    [McpServerTool(Name = "search_duende_blog", Title = "Search Duende Blog")]
    [Description("Semantically search within the Duende blog for the given query.")]
    public async Task<string> Search(
        [Description("The search query. Keep it concise and specific to increase the likelihood of a match.")] string query)
    {
        var results = await db.FTSBlogArticle
            .FromSqlRaw("SELECt * FROM FTSBlogArticle WHERE Title MATCH {0} OR Content MATCH {0} ORDER BY rank", McpDb.EscapeFtsQueryString(query))
            .AsNoTracking()
            .Take(6)
            .ToListAsync();

        var responseBuilder = new StringBuilder();
        responseBuilder.Append(CultureInfo.InvariantCulture, $"## Query\n\n{query}\n\n");

        if (results.Count == 0)
        {
            responseBuilder.Append(CultureInfo.InvariantCulture, $"## Response\n\nNo results found for: \"{query}\"\n\nIf you'd like to retry the search, try changing the query to increase the likelihood of a match.");
            return responseBuilder.ToString();
        }

        responseBuilder.Append(CultureInfo.InvariantCulture, $"## Response\n\nResults found for: \"{query}\". Listing a document id and document title:\n\n");

        foreach (var result in results)
        {
            responseBuilder.Append(CultureInfo.InvariantCulture, $"- [{result.Id}]({result.Title})\n");
        }

        return responseBuilder.ToString();
    }

    [McpServerTool(Name = "fetch_duende_blog", Title = "Fetch specific article from Duende blog")]
    [Description("Fetch a specific article from the Duende blog.")]
    public async Task<string> Fetch([Description("The document id.")] string id)
    {
        var result = await db.FTSBlogArticle
            .FromSqlRaw("SELECt * FROM FTSBlogArticle WHERE Id = {0} ORDER BY rank", id)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        return result == null
            ? $"No data found for document: \"{id}\"."
            : $"# {result.Title}\n\n{result.Content}";
    }
}
