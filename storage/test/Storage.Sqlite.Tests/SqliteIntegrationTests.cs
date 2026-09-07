// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Sqlite;

namespace Duende.Storage.IntegrationTests;

public partial class Stores
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class StoreBatchOperations
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class StoreLinkOperations
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class StoreLinkQueryTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class StoreOutboxOperations
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class StoreTtlTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class StoreTryReadManyTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class PurgeExpiredTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class PurgePoolTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class FilterTranslatorIntegrationTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreArrayFilterTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreBasicExpressionTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreCountTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreCursorPagingTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreCursorBidirectionalPagingTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreGuidFieldTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStorePagingTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreSortTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class SystemTimestampQueryTests
{
    private IStorageFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

