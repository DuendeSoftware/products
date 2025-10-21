// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace Documentation.Mcp.Database;

internal class FTSBlogArticle
{
    [Key]
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Content { get; init; }
}
