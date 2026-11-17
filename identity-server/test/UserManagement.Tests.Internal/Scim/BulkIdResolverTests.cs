// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement;
using Duende.UserManagement.Scim.Internal.Endpoints.Bulk;

namespace Duende.Platform.UserManagement.Scim;

public sealed class BulkIdResolverTests
{
    [Fact]
    public void resolve_path_with_no_bulkId_reference_returns_true_unchanged()
    {
        var resolver = new BulkIdResolver();
        var path = "/Users/some-real-id";

        var result = resolver.TryResolvePath(ref path);

        result.ShouldBeTrue();
        path.ShouldBe("/Users/some-real-id");
    }

    [Fact]
    public void resolve_path_with_registered_bulkId_substitutes_id()
    {
        var resolver = new BulkIdResolver();
        resolver.Register("user1", "real-id-abc");

        var path = "/Users/bulkId:user1";
        var result = resolver.TryResolvePath(ref path);

        result.ShouldBeTrue();
        path.ShouldBe("/Users/real-id-abc");
    }

    [Fact]
    public void resolve_path_with_unregistered_bulkId_returns_false()
    {
        var resolver = new BulkIdResolver();

        var path = "/Users/bulkId:unknown";
        var result = resolver.TryResolvePath(ref path);

        result.ShouldBeFalse();
    }

    [Fact]
    public void register_overwrites_previous_mapping()
    {
        var resolver = new BulkIdResolver();
        resolver.Register("x", "id-v1");
        resolver.Register("x", "id-v2");

        var path = "/Users/bulkId:x";
        _ = resolver.TryResolvePath(ref path);

        path.ShouldBe("/Users/id-v2");
    }

    [Fact]
    public void resolve_json_text_with_no_bulkId_returns_unchanged()
    {
        var resolver = new BulkIdResolver();
        var json = "{\"userName\":\"alice\"}";

        var result = resolver.ResolveJsonText(json);

        result.ShouldBe(json);
    }

    [Fact]
    public void resolve_json_text_substitutes_bulkId_in_string_value()
    {
        var resolver = new BulkIdResolver();
        resolver.Register("u1", "real-user-id");

        var result = resolver.ResolveJsonText("{\"value\":\"bulkId:u1\"}");

        _ = result.ShouldNotBeNull();
        ShouldlyExtensions.ShouldContain(result, "real-user-id");
        result.ShouldNotContain("bulkId:");
    }

    [Fact]
    public void resolve_json_text_with_unresolvable_bulkId_returns_null()
    {
        var resolver = new BulkIdResolver();

        var result = resolver.ResolveJsonText("{\"value\":\"bulkId:missing\"}");

        result.ShouldBeNull();
    }

    [Fact]
    public void resolve_json_text_substitutes_nested_bulkId()
    {
        var resolver = new BulkIdResolver();
        resolver.Register("g1", "real-group-id");

        var json = "{\"members\":[{\"type\":\"Group\",\"value\":\"bulkId:g1\"}]}";

        var result = resolver.ResolveJsonText(json);

        _ = result.ShouldNotBeNull();
        ShouldlyExtensions.ShouldContain(result, "real-group-id");
        result.ShouldNotContain("bulkId:");
    }

    [Fact]
    public void resolve_json_text_returns_null_if_nested_bulkId_unresolvable()
    {
        var resolver = new BulkIdResolver();

        var json = "{\"members\":[{\"value\":\"bulkId:notRegistered\"}]}";

        var result = resolver.ResolveJsonText(json);

        result.ShouldBeNull();
    }

    [Fact]
    public void get_bulkId_key_returns_key_from_prefixed_string()
    {
        var key = BulkIdResolver.GetBulkIdKey("bulkId:abc123");

        key.ShouldBe("abc123");
    }

    [Fact]
    public void get_bulkId_key_returns_null_for_non_prefixed_string()
    {
        var key = BulkIdResolver.GetBulkIdKey("some-real-id");

        key.ShouldBeNull();
    }
}
