// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel;
using System.Text;
using Documentation.Mcp.Database;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Server;

namespace Documentation.Mcp.Sources.Samples;

[McpServerToolType]
internal class SamplesSearchTool(McpDb db)
{
    [McpServerTool(Name = "search_duende_samples", Title = "Search Duende Code Samples")]
    [Description("Search within the Duende code samples for the given query. Use this tool to find recent and relevant C# code samples.")]
    public async Task<string> Search(
        [Description("The search query. Keep it concise and specific to increase the likelihood of a match.")] string query)
    {
        var results = await db.FTSSampleProject
            .FromSqlRaw("SELECT * FROM FTSSampleProject WHERE Title MATCH {0} OR Description MATCH {0} OR Product MATCH {0} ORDER BY rank", McpDb.EscapeFtsQueryString(query, "OR"))
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

        responseBuilder.Append($"## Response\n\nResults found for: \"{query}\". Listing a document id and document title, followed by related product and a description of the sample:\n\n");

        foreach (var result in results)
        {
            responseBuilder.Append($"- [{result.Id}]({result.Title}) ({result.Product}) - Description: {result.Description}\n");
        }

        return responseBuilder.ToString();
    }

    [McpServerTool(Name = "fetch_duende_sample", Title = "Fetch specific sample from Duende Code Samples", UseStructuredContent = true)]
    [Description("Fetch a specific sample from the Duende Code Samples. The result contains a title, description, and the sample code in a list of files.")]
    public async Task<SampleProject> Fetch(
        [Description("The document id.")] string id)
    {
        var result = await db.FTSSampleProject
            .FromSqlRaw("SELECT * FROM FTSSampleProject WHERE Id = {0} ORDER BY rank", id)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (result == null)
        {
            return SampleProject.NotFound();
        }

        return new SampleProject
        {
            Title = result.Title,
            Description = result.Description,
            Files = result.Files.Select(it => new SampleProjectFile { Content = it }).ToList()
        };
    }

    [McpServerTool(Name = "fetch_duende_sample_file", Title = "Fetch a file from a specific sample from Duende Code Samples", UseStructuredContent = true)]
    [Description("Fetch a file from specific sample from the Duende Code Samples.")]
    public async Task<SampleProjectFile> FetchFile(
        [Description("The document id.")] string id,
        [Description("The file name.")] string filename)
    {
        filename = filename.Replace("wwwroot", "~");

        var result = await db.FTSSampleProject
            .FromSqlRaw("SELECT * FROM FTSSampleProject WHERE Id = {0} ORDER BY rank", id)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (result == null)
        {
            return SampleProjectFile.NotFound();
        }

        var files = result.Files.Select(it => new SampleProjectFile { Content = it }).ToList();
        return files.FirstOrDefault(it => it.Content.Contains(filename, StringComparison.OrdinalIgnoreCase))
               ?? SampleProjectFile.NotFound();
    }

    internal class SampleProject
    {
        public static SampleProject NotFound() => new SampleProject { Title = "No data found.", Description = "" };

        public required string Title { get; set; }
        public required string Description { get; set; }
        public List<SampleProjectFile> Files { get; set; } = new(0);
    }

    internal class SampleProjectFile
    {
        public static SampleProjectFile NotFound() => new SampleProjectFile { Content = "No data found." };

        public required string Content { get; set; }
    }
}
