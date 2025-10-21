// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Documentation.Mcp.Database;

internal class McpDb(DbContextOptions<McpDb> options) : DbContext(options)
{
    public DbSet<State> State => Set<State>();

    public DbSet<FTSDocsArticle> FTSDocsArticle => Set<FTSDocsArticle>();

    public DbSet<FTSBlogArticle> FTSBlogArticle => Set<FTSBlogArticle>();

    public DbSet<FTSSampleProject> FTSSampleProject => Set<FTSSampleProject>();

    public async Task SetLastUpdateStateAsync(string key, DateTimeOffset value)
    {
        var stateEntity = await State.FirstOrDefaultAsync(it => it.Key == key);
        if (stateEntity == null)
        {
            State.Add(new State
            {
                Id = Guid.NewGuid().ToString(),
                Key = key,
                Value = JsonSerializer.Serialize(value)
            });
        }
        else
        {
            stateEntity.Value = JsonSerializer.Serialize(value);
        }

        await SaveChangesAsync();
    }

    public async Task<DateTimeOffset> GetLastUpdateStateAsync(string key)
    {
        var stateEntity = await State.FirstOrDefaultAsync(it => it.Key == key);
        return stateEntity == null
            ? DateTimeOffset.MinValue
            : JsonSerializer.Deserialize<DateTimeOffset>(stateEntity.Value);
    }

    public static string? EscapeFtsQueryString(string? query)
        => !string.IsNullOrEmpty(query)
            ? string.Join(" ", query.Split(' ').Select(q => $"\"{q.Replace("\"", "\"\"")}\""))
            : query;

    public static string? EscapeFtsQueryString(string? query, string joinWith)
        => !string.IsNullOrEmpty(query)
            ? string.Join($" {joinWith} ", query.Split(' ').Select(q => $"\"{q.Replace("\"", "\"\"")}\""))
            : query;
}
