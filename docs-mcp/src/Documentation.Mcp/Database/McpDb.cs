// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Documentation.Mcp.Database;

internal sealed class McpDb(DbContextOptions<McpDb> options) : DbContext(options)
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
            _ = State.Add(new State
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

        _ = await SaveChangesAsync();
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
            ? string.Join(" ", query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(q => $"\"{q.Replace("\"", "\"\"", StringComparison.OrdinalIgnoreCase)}\""))
            : query;

    public static string? EscapeFtsQueryString(string? query, string joinWith)
        => !string.IsNullOrEmpty(query)
            ? string.Join($" {joinWith} ", query.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(q => $"\"{q.Replace("\"", "\"\"", StringComparison.OrdinalIgnoreCase)}\""))
            : query;

    // FTS5 stores Files as a JSON text column — convert to/from List<string>
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        _ = modelBuilder.Entity<FTSSampleProject>()
            .Property(e => e.Files)
            .HasConversion(new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonSerializerOptions.Default) ?? new List<string>()));
}
