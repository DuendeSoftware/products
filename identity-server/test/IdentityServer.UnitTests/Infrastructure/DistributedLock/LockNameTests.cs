// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
#nullable enable

using Duende.IdentityServer.Infrastructure.DistributedLock;
using Duende.Spaces;

namespace IdentityServer.UnitTests.Infrastructure.DistributedLock;

public class LockNameTests
{
    [Fact]
    public void outbox_processor_has_the_expected_value() =>
        LockName.OutboxProcessor.Value.ShouldBe("jobs/outbox-processor");

    [Fact]
    public void storage_purge_has_the_expected_value() =>
        LockName.StoragePurge.Value.ShouldBe("jobs/storage-purge");

    [Fact]
    public void server_side_session_cleanup_has_the_expected_value() =>
        LockName.ServerSideSessionCleanup.Value.ShouldBe("jobs/server-side-session-cleanup");

    [Fact]
    public void signing_keys_formats_the_space_id_as_lowercase_dashed_guid()
    {
        var spaceId = SpaceId.New();

        var name = LockName.SigningKeys(spaceId);

        name.Value.ShouldBe($"signing-keys/{spaceId.Value:D}");
    }

    [Fact]
    public void a_stored_value_can_be_loaded_and_matches_the_original()
    {
        var original = LockName.OutboxProcessor;

        var loaded = LockName.Load(original.Value);

        loaded.ShouldBe(original);
        loaded.Value.ShouldBe("jobs/outbox-processor");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Jobs/Outbox-Processor")] // uppercase not allowed
    [InlineData("jobs/outbox_processor")] // underscore not allowed
    [InlineData("jobs/outbox processor")] // space not allowed
    public void invalid_characters_are_rejected(string value) =>
        LockName.TryCreate(value, out _, out _).ShouldBeFalse();

    [Fact]
    public void too_long_value_is_rejected()
    {
        var tooLong = new string('a', 101);

        LockName.TryCreate(tooLong, out _, out var errors).ShouldBeFalse();
        errors.ShouldNotBeNull();
    }

    [Fact]
    public void valid_value_can_be_created()
    {
        LockName.TryCreate("jobs/custom-job-123", out var result).ShouldBeTrue();
        result.ShouldNotBeNull();
        result.Value.ShouldBe("jobs/custom-job-123");
    }
}
