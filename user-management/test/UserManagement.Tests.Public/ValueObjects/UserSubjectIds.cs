// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;

namespace Duende.Platform.UserManagement.ValueObjects;

public static class UserSubjectIds
{
    [Fact]
    public static void Guid_is_id()
    {
        var id = Guid.NewGuid();
        var instance = UserSubjectId.Create(id.ToString());

        var guid = Guid.Parse(instance.Value);

        guid.ShouldBe(id);
    }

    [Fact]
    public static void String_is_id_string()
    {
        var id = Guid.NewGuid();
        var instance = UserSubjectId.Create(id.ToString());

        var @string = instance.ToString();

        @string.ShouldBe(id.ToString());
    }

    [Fact]
    public static void Can_parse_arbitrary_string()
    {
        var value = "user@example.com";
        var instance = UserSubjectId.Create(value);

        instance.Value.ShouldBe(value);
    }

    [Fact]
    public static void Rejects_empty_string()
    {
        var ex = Record.Exception(() => _ = UserSubjectId.Create(""));

        _ = ex.ShouldBeOfType<FormatException>();
    }
}
