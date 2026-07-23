// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Sqlite;

namespace Duende.Storage.IntegrationTests;

public partial class Stores
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class StoreBatchOperations
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class StoreLinkOperations
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class StoreLinkQueryTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class StoreOutboxOperations
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class StoreTtlTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class StoreTryReadManyTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class PurgeExpiredTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class PurgePoolTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class FilterTranslatorIntegrationTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreArrayFilterTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreBasicExpressionTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreCountTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreCursorPagingTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreCursorBidirectionalPagingTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreGuidFieldTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStorePagingTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class QueryStoreSortTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

public partial class SystemTimestampQueryTests
{
    private IStoreFixtureFactory FixtureFactory { get; } = new SqliteStoreFixtureFactory();
}

