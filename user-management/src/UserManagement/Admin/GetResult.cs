// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Diagnostics.CodeAnalysis;

#pragma warning disable CA1000 // Do not declare static members on generic types - Factory methods are intended API design

namespace Duende.UserManagement.Admin;

public record GetResult<TDto>
{
    [MemberNotNullWhen(true, nameof(Item), nameof(Version))]
    public bool Found { get; internal set; }
    public TDto? Item { get; internal set; }
    public int? Version { get; internal set; }
}

public static class GetResult
{
    public static GetResult<TDto> Found<TDto>(TDto item, int version) =>
        new()
        {
            Found = true,
            Item = item,
            Version = version
        };

    public static GetResult<TDto> NotFound<TDto>() =>
        new()
        {
            Found = false
        };
}
