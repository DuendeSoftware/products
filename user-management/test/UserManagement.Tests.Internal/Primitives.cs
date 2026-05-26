// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.UserManagement;

namespace Duende.Platform.UserManagement;

public static class Primitives
{
    [Theory]
    [InlineData(0x0)]
    [InlineData(0x1)]
    [InlineData(0x2)]
    [InlineData(0x3)]
    [InlineData(0x4)]
    [InlineData(0x5)]
    [InlineData(0x6)]
    [InlineData(0x7)]
    [InlineData(0xC)]
    [InlineData(0xD)]
    [InlineData(0xE)]
    [InlineData(0xF)]
    public static void UuidV7CannotParseInvalidGuidVariants(byte variant)
    {
        var value = GuidFactory.Create(7, variant);

        var exception = Record.Exception(() => _ = UuidV7.From(value));

        _ = exception.ShouldBeOfType<FormatException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(16)]
    public static void UuidV7CannotParseInvalidGuidVersions(byte version)
    {
        var value = GuidFactory.Create(version);

        var exception = Record.Exception(() => _ = UuidV7.From(value));

        _ = exception.ShouldBeOfType<FormatException>();
    }
}
