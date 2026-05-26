// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Scim.Internal.Endpoints.Bulk;

namespace Duende.Platform.UserManagement.Scim;

public sealed class BulkOperationRouterTests
{
    [Fact]
    public void post_to_users_is_valid()
    {
        var result = BulkOperationRouter.Parse("POST", "/Users");

        result.IsValid.ShouldBeTrue();
        result.ResourceType.ShouldBe("User");
        result.ResourceId.ShouldBeNull();
    }

    [Fact]
    public void post_to_groups_is_valid()
    {
        var result = BulkOperationRouter.Parse("POST", "/Groups");

        result.IsValid.ShouldBeTrue();
        result.ResourceType.ShouldBe("Group");
        result.ResourceId.ShouldBeNull();
    }

    [Fact]
    public void post_with_id_is_invalid()
    {
        var result = BulkOperationRouter.Parse("POST", "/Users/some-id");

        result.IsValid.ShouldBeFalse();
        result.ErrorDetail.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void put_to_users_with_id_is_valid()
    {
        var id = Guid.NewGuid().ToString();
        var result = BulkOperationRouter.Parse("PUT", $"/Users/{id}");

        result.IsValid.ShouldBeTrue();
        result.ResourceType.ShouldBe("User");
        result.ResourceId.ShouldBe(id);
    }

    [Fact]
    public void put_without_id_is_invalid()
    {
        var result = BulkOperationRouter.Parse("PUT", "/Users");

        result.IsValid.ShouldBeFalse();
        result.ErrorDetail.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void patch_to_groups_with_id_is_valid()
    {
        var id = Guid.NewGuid().ToString();
        var result = BulkOperationRouter.Parse("PATCH", $"/Groups/{id}");

        result.IsValid.ShouldBeTrue();
        result.ResourceType.ShouldBe("Group");
        result.ResourceId.ShouldBe(id);
    }

    [Fact]
    public void patch_without_id_is_invalid()
    {
        var result = BulkOperationRouter.Parse("PATCH", "/Groups");

        result.IsValid.ShouldBeFalse();
        result.ErrorDetail.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void delete_to_users_with_id_is_valid()
    {
        var id = Guid.NewGuid().ToString();
        var result = BulkOperationRouter.Parse("DELETE", $"/Users/{id}");

        result.IsValid.ShouldBeTrue();
        result.ResourceType.ShouldBe("User");
        result.ResourceId.ShouldBe(id);
    }

    [Fact]
    public void delete_without_id_is_invalid()
    {
        var result = BulkOperationRouter.Parse("DELETE", "/Users");

        result.IsValid.ShouldBeFalse();
        result.ErrorDetail.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void path_with_bulkId_reference_is_valid_with_id_preserved()
    {
        var result = BulkOperationRouter.Parse("PATCH", "/Users/bulkId:abc123");

        result.IsValid.ShouldBeTrue();
        result.ResourceType.ShouldBe("User");
        result.ResourceId.ShouldBe("bulkId:abc123");
    }

    [Fact]
    public void delete_with_bulkId_reference_is_valid()
    {
        var result = BulkOperationRouter.Parse("DELETE", "/Groups/bulkId:grp1");

        result.IsValid.ShouldBeTrue();
        result.ResourceType.ShouldBe("Group");
        result.ResourceId.ShouldBe("bulkId:grp1");
    }

    [Fact]
    public void unknown_resource_type_is_invalid()
    {
        var result = BulkOperationRouter.Parse("POST", "/Unknown");

        result.IsValid.ShouldBeFalse();
        result.ErrorDetail.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void get_method_is_invalid()
    {
        var result = BulkOperationRouter.Parse("GET", "/Users");

        result.IsValid.ShouldBeFalse();
        result.ErrorDetail.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void method_is_case_insensitive()
    {
        var result = BulkOperationRouter.Parse("post", "/Users");

        result.IsValid.ShouldBeTrue();
        result.ResourceType.ShouldBe("User");
    }

    [Fact]
    public void path_resource_type_is_case_insensitive()
    {
        var result = BulkOperationRouter.Parse("POST", "/users");

        result.IsValid.ShouldBeTrue();
        result.ResourceType.ShouldBe("User");
    }

    [Fact]
    public void path_without_leading_slash_is_valid()
    {
        var result = BulkOperationRouter.Parse("POST", "Users");

        result.IsValid.ShouldBeTrue();
        result.ResourceType.ShouldBe("User");
    }
}
