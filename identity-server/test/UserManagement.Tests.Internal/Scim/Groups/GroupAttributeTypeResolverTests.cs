// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal.Querying.Fields;
using Duende.UserManagement;
using Duende.UserManagement.Membership.Internal.Storage;

namespace Duende.Platform.UserManagement.Scim.Groups;

public sealed class GroupAttributeTypeResolverTests
{
    private readonly GroupAttributeTypeResolver _resolver = new();

    [Fact]
    public void ResolvesDisplayNameToStringField()
    {
        var field = _resolver.ResolveField("displayName");
        _ = field.ShouldBeOfType<StringField>();
        field.Path.ShouldBe("NAME");
    }

    [Theory]
    [InlineData("DisplayName")]
    [InlineData("DISPLAYNAME")]
    [InlineData("displayname")]
    public void DisplayNameResolutionIsCaseInsensitive(string AttributeCode)
    {
        var field = _resolver.ResolveField(AttributeCode);
        _ = field.ShouldBeOfType<StringField>();
        field.Path.ShouldBe("NAME");
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("members")]
    [InlineData("description")]
    [InlineData("id")]
    [InlineData("")]
    public void UnknownAttributeThrowsNotSupportedException(string AttributeCode)
    {
        var ex = Should.Throw<NotSupportedException>(() => _resolver.ResolveField(AttributeCode));
        ShouldlyExtensions.ShouldContain(ex.Message, AttributeCode);
    }
}
