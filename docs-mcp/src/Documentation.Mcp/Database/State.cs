// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.ComponentModel.DataAnnotations;

namespace Documentation.Mcp.Database;

internal class State
{
    [Key]
    public required string Id { get; init; }
    public required string Key { get; init; }
    public required string Value { get; set; }
}
