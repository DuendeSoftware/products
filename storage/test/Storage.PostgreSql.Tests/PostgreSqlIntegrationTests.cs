// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.PostgreSql;

namespace Duende.Storage.IntegrationTests;

[Collection("PostgreSqlIntegration")]
public partial class Stores(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class StoreBatchOperations(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class StoreLinkOperations(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class StoreLinkQueryTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class StoreOutboxOperations(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class StoreTtlTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class StoreTryReadManyTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class PurgeExpiredTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class PurgePoolTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class FilterTranslatorIntegrationTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class QueryStoreArrayFilterTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class QueryStoreBasicExpressionTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class QueryStoreCountTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class QueryStoreCursorPagingTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class QueryStoreCursorBidirectionalPagingTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class QueryStoreGuidFieldTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class QueryStorePagingTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class QueryStoreSortTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

[Collection("PostgreSqlIntegration")]
public partial class SystemTimestampQueryTests(AspireFixture fixture)
{
    private IStorageFixtureFactory FixtureFactory { get; } = new PostgreSqlStoreFixtureFactory(fixture);
}

