// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Internal.Storage;

namespace Duende.Platform.UserManagement;

public sealed class UserDsoTests
{
    [Fact]
    public void add_or_update_aspect_ref_adds_new_aspect_when_not_present()
    {
        var user = new UserDso.V1(Guid.NewGuid(), Guid.NewGuid().ToString(), null, []);
        var aspectRef = new UserDso.AspectRef(Guid.NewGuid(), 1, 1000u);

        var updated = UserRepository.AddOrUpdateAspectRef(user, aspectRef);

        _ = updated.Aspects.ShouldHaveSingleItem();
        updated.Aspects[0].ShouldBe(aspectRef);
    }

    [Fact]
    public void add_or_update_aspect_ref_updates_existing_aspect_with_same_entity_type()
    {
        var existingRef = new UserDso.AspectRef(Guid.NewGuid(), 1, 1000u);
        var user = new UserDso.V1(Guid.NewGuid(), Guid.NewGuid().ToString(), null, [existingRef]);
        var updatedRef = new UserDso.AspectRef(existingRef.Id, 2, 1000u);

        var updated = UserRepository.AddOrUpdateAspectRef(user, updatedRef);

        _ = updated.Aspects.ShouldHaveSingleItem();
        updated.Aspects[0].Version.ShouldBe(2);
    }

    [Fact]
    public void add_or_update_aspect_ref_preserves_other_aspects()
    {
        var profileRef = new UserDso.AspectRef(Guid.NewGuid(), 1, 1500u);
        var user = new UserDso.V1(Guid.NewGuid(), Guid.NewGuid().ToString(), null, [profileRef]);
        var authRef = new UserDso.AspectRef(Guid.NewGuid(), 1, 1000u);

        var updated = UserRepository.AddOrUpdateAspectRef(user, authRef);

        updated.Aspects.Count.ShouldBe(2);
        updated.Aspects.ShouldContain(profileRef);
        updated.Aspects.ShouldContain(authRef);
    }

    [Fact]
    public void add_or_update_aspect_ref_does_not_mutate_original()
    {
        var user = new UserDso.V1(Guid.NewGuid(), Guid.NewGuid().ToString(), null, []);
        var aspectRef = new UserDso.AspectRef(Guid.NewGuid(), 1, 1000u);

        _ = UserRepository.AddOrUpdateAspectRef(user, aspectRef);

        user.Aspects.ShouldBeEmpty();
    }

    [Fact]
    public void user_dso_entity_type_has_id_900() =>
        UserDso.EntityType.Id.ShouldBe(900u);

    [Fact]
    public void user_dso_v1_dso_version_references_correct_entity_type() =>
        UserDso.V1.DsoVersion.EntityType.ShouldBe(UserDso.EntityType);

    [Fact]
    public void user_dso_v1_dso_version_has_schema_version_1() =>
        UserDso.V1.DsoVersion.SchemaVersion.ShouldBe(1u);
}
