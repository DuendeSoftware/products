// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;

namespace Duende.UserManagement;

/// <summary>
/// Test DSO representing a user with email addresses.
/// Used across all store implementation tests.
/// </summary>
internal sealed record TestUserDso : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(2, "UserEntity"), 1);

    public string Name { get; init; } = string.Empty;
    public EmailAddressType[]? Emails { get; init; }
}

/// <summary>
/// Email address type for testing array filters.
/// </summary>
internal sealed record EmailAddressType
{
    public string Type { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public DateTimeOffset? CreatedAt { get; init; }
    public int? Priority { get; init; }
}

/// <summary>
/// Test DSO for basic expression tests with various field types.
/// </summary>
internal sealed record TestEntityDso : IDataStorageObject
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
internal sealed record TestSortDso : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(4, "SortTestEntity"), 1);

    public string Name { get; init; } = string.Empty;
    public int? Rank { get; init; }
    public decimal? Rating { get; init; }
    public DateTimeOffset? Timestamp { get; init; }
    public string? Category { get; init; }
}

/// <summary>
/// Test DSO for Guid and ExactMatch field tests.
/// </summary>
internal sealed record TestGuidEntityDso : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } = new(new EntityType(6, "GuidTestEntity"), 1);

    public string Name { get; init; } = string.Empty;
    public Guid? ResourceId { get; init; }
    public string? ApiKey { get; init; }
    public string? Tag { get; init; }
}
