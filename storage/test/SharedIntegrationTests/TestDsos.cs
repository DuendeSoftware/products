// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.Storage.IntegrationTests;

public sealed record TestDso(string Value) : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(99, nameof(TestDso)), 1);
}

public enum TestKeys
{
    JsonKey = 1,
    UuidV4Key = 2,
    UuidV7Key = 3
}

public sealed record TestJsonKeyDsk(string JsonValue) : IDataStorageKey
{
    public static DataStorageKeyVersion DskVersion { get; } = new(TestKeys.JsonKey, 1);
}

public sealed record TestUuidV4KeyDsk(Guid Value) : IGuidDataStorageKey
{
    public static DataStorageKeyVersion DskVersion { get; } = new(TestKeys.UuidV4Key, 1);
}

public sealed record TestUuidV7KeyDsk(Guid Value) : IGuidDataStorageKey
{
    public static DataStorageKeyVersion DskVersion { get; } = new(TestKeys.UuidV7Key, 1);
}

/// <summary>
/// Test DSO for batch operations testing with a different entity type.
/// </summary>
public sealed record TestDso2(string Value) : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(100, nameof(TestDso2)), 1);
}

/// <summary>
/// Test DSO representing a user with email addresses.
/// </summary>
public sealed record TestUserDso : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(2, "UserEntity"), 1);

    public string Name { get; init; } = string.Empty;

    public EmailAddress[]? Emails { get; init; }
}

/// <summary>
/// Email address type for testing array filters.
/// </summary>
public sealed record EmailAddress
{
    public string Type { get; init; } = string.Empty;

    public string Value { get; init; } = string.Empty;

    public DateTimeOffset? CreatedAt { get; init; }

    public int? Priority { get; init; }
}

/// <summary>
/// Test DSO for basic expression tests with various field types.
/// </summary>
public sealed record TestEntityDso : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(3, "TestEntity"), 1);

    public string Name { get; init; } = string.Empty;
    public int? Score { get; init; }
    public decimal? Price { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public DateTimeOffset? LastLogin { get; init; }
    public bool? IsActive { get; init; }
    public string? Status { get; init; }
}

/// <summary>
/// Test DSO for sorting tests with various sortable field types.
/// </summary>
public sealed record TestSortDso : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(4, "SortTestEntity"), 1);

    public string Name { get; init; } = string.Empty;
    public int? Rank { get; init; }
    public decimal? Rating { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public string? Category { get; init; }
}

/// <summary>
/// Test DSO for cursor pagination tests.
/// </summary>
public sealed record TestCursorDso : IDataStorageObject
{
    public required string Name { get; init; }
    public required int Rank { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required bool IsActive { get; init; }

    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(5, nameof(TestCursorDso)), 1);
}

/// <summary>
/// Test DSO for offset-based pagination tests.
/// </summary>
public sealed record TestPageDso : IDataStorageObject
{
    public required string Name { get; init; }
    public required int Rank { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required bool IsActive { get; init; }

    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(6, nameof(TestPageDso)), 1);
}

/// <summary>
/// Test DSO for Guid and ExactMatch field tests.
/// </summary>
public sealed record TestGuidEntityDso : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(6, "GuidTestEntity"), 1);

    public string Name { get; init; } = string.Empty;
    public Guid? ResourceId { get; init; }
    public string? ApiKey { get; init; }
    public string? Tag { get; init; }
}

// Link-related DSOs for link query tests

public sealed record UserDso(string Name) : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(95, nameof(UserDso)), 1);
}

public sealed record RoleDso(string Name) : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(96, nameof(RoleDso)), 1);
}

public sealed record GroupDso(string Name) : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(97, nameof(GroupDso)), 1);
}

/// <summary>
/// Shared link definitions used by link operation and outbox tests.
/// Uses TestDso (entity type 99) as "left" and TestDso2 (entity type 100) as "right".
/// </summary>
public static class TestLinkData
{
    public static readonly LinkDefinition TestLink = new()
    {
        Left = TestDso.DsoVersion.EntityType,
        Right = TestDso2.DsoVersion.EntityType,
        Link = LinkTypeRegistry.MembershipRole
    };

    public static readonly LinkDefinition TestLink2 = new()
    {
        Left = TestDso2.DsoVersion.EntityType,
        Right = TestDso.DsoVersion.EntityType,
        Link = LinkTypeRegistry.GroupRole
    };
}
