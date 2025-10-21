// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Text;
using Documentation.Mcp.Database;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Documentation.Mcp.Sources.Docs;

[McpServerToolType]
public class DocsSearchTool(McpDb db)
{
    [McpServerTool(Name = "search_duende_docs", Title = "Search Duende Documentation")]
    [Description("Semantically search within the Duende documentation for the given query.")]
    public async Task<string> Search(
        [Description("The search query. Keep it concise and specific to increase the likelihood of a match.")] string query)
    {
        var results = await db.FTSDocsArticle
            .FromSqlRaw("SELECT * FROM FTSDocsArticle WHERE Title MATCH {0} OR Content MATCH {0} OR Product MATCH {0} ORDER BY rank", db.EscapeFtsQueryString(query))
            .AsNoTracking()
            .Take(6)
            .ToListAsync();

        var responseBuilder = new StringBuilder();
        responseBuilder.Append($"## Query\n\n{query}\n\n");

        if (results.Count == 0)
        {
            responseBuilder.Append($"## Response\n\nNo results found for: \"{query}\"\n\nIf you'd like to retry the search, try changing the query to increase the likelihood of a match.");
            return responseBuilder.ToString();
        }

        responseBuilder.Append($"## Response\n\nResults found for: \"{query}\". Listing a document id and document title, followed by related product:\n\n");

        foreach (var result in results)
        {
            responseBuilder.Append($"- [{result.Id}]({result.Title}) ({result.Product})\n");
        }

        return responseBuilder.ToString();
    }

    [McpServerTool(Name = "fetch_duende_docs", Title = "Fetch specific article from Duende Documentation")]
    [Description("Fetch a specific article from the Duende documentation.")]
    public async Task<string> Fetch(
        [Description("The document id.")] string id)
    {
        var result = await db.FTSDocsArticle
            .FromSqlRaw("SELECT * FROM FTSDocsArticle WHERE Id = {0} ORDER BY rank", id)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (result == null)
        {
            return $"No data found for document: \"{id}\".";
        }

        return result.Content;
    }
}
