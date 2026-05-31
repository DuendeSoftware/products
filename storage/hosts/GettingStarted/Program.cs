// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

// ──────────────────────────────────────────────────────────────
// 1. DI Setup — register the Sqlite store and our custom DSO
// ──────────────────────────────────────────────────────────────

var services = new ServiceCollection();
services.AddLogging();

services.AddStorageInternal(storage => storage.AddSqliteStore(options =>
{
    options.ConnectionString = "Data Source=sample.db";
}));

services.AddDsoRegistration<ProductDso>();

var provider = services.BuildServiceProvider();

// ──────────────────────────────────────────────────────────────
// 2. Create schema and open a pool (logical tenant)
// ──────────────────────────────────────────────────────────────

var pooledStore = provider.GetRequiredService<IPooledStore>();
await pooledStore.MigrateAsync(CancellationToken.None);

var store = pooledStore.OpenPool(1); // Pool 1 — think of it as a tenant

Console.WriteLine("✓ Store initialized with SQLite\n");

// ──────────────────────────────────────────────────────────────
// 3. Create entities with search fields and alternate keys
// ──────────────────────────────────────────────────────────────

var products = new (string Sku, string Name, decimal Price, bool InStock)[]
{
    ("SKU-001", "Mechanical Keyboard", 149.99m, true),
    ("SKU-002", "Ergonomic Mouse", 79.99m, true),
    ("SKU-003", "USB-C Hub", 49.99m, false),
    ("SKU-004", "Monitor Stand", 129.99m, true),
    ("SKU-005", "Desk Lamp", 39.99m, true),
};

foreach (var p in products)
{
    var id = UuidV7.New();
    var dso = new ProductDso(p.Name, p.Sku, p.Price, p.InStock);

    // Search fields enable filtering/sorting without deserializing the full entity
    var searchFields = new SearchFieldsBuilder()
        .Add("name", p.Name)
        .Add("price", p.Price)
        .Add("inStock", p.InStock)
        .Build();

    // Alternate key — look up products by SKU
    var keys = new[] { DataStorageKey.Create(new SkuKey(p.Sku)) };

    var result = await store.CreateAsync(
        id,
        dso,
        keys,
        searchFields,
        Expiration.NoExpiration,
        outboxEvents: [],
        CancellationToken.None);

    Console.WriteLine(result == CreateResult.Success
        ? $"  Created: {p.Name} ({p.Sku}) — ${p.Price}"
        : $"  Skipped (already exists): {p.Name}");
}

Console.WriteLine();

// ──────────────────────────────────────────────────────────────
// 4. Read by alternate key (SKU lookup)
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Read by SKU key ──");
var skuLookup = await store.TryReadAsync(
    ProductDso.DsoVersion.EntityType,
    DataStorageKey.Create(new SkuKey("SKU-002")),
    CancellationToken.None);

if (skuLookup.Found)
{
    var product = (ProductDso)skuLookup.Dso;
    Console.WriteLine($"  Found: {product.Name} — ${product.Price} (version {skuLookup.Version})\n");
}

// ──────────────────────────────────────────────────────────────
// 5. Query with filtering — find in-stock items over $50
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Query: in-stock products over $50 ──");

var priceField = new NumberField("price");
var inStockField = new BooleanField("inStock");
var filter = new AndExpression(
    priceField.GreaterThan(50m),
    inStockField.IsTrue());

var queryResult = await store.QueryAsync<ProductDso>(
    ProductDso.DsoVersion.EntityType,
    filter,
    new SortParameter(priceField, SortDirection.Ascending),
    PagedDataRange.First((DataRangeSize)10),
    CancellationToken.None);

foreach (var envelope in queryResult)
{
    Console.WriteLine($"  {envelope.Value.Name} — ${envelope.Value.Price}");
}

Console.WriteLine();

// ──────────────────────────────────────────────────────────────
// 6. Query with string filtering — name contains "Mouse"
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Query: name contains 'Mouse' ──");

var nameField = new StringField("name");
var nameFilter = nameField.Contains("Mouse");

var mouseResult = await store.QueryAsync<ProductDso>(
    ProductDso.DsoVersion.EntityType,
    nameFilter,
    SortParameter.Empty,
    PagedDataRange.First((DataRangeSize)10),
    CancellationToken.None);

foreach (var envelope in mouseResult)
{
    Console.WriteLine($"  {envelope.Value.Name} — SKU: {envelope.Value.Sku}");
}

Console.WriteLine();

// ──────────────────────────────────────────────────────────────
// 7. Update with optimistic concurrency
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Update with optimistic concurrency ──");

if (skuLookup.Found)
{
    var original = (ProductDso)skuLookup.Dso;
    var updated = original with { Price = 69.99m };

    var updateSearchFields = new SearchFieldsBuilder()
        .Add("name", updated.Name)
        .Add("price", updated.Price)
        .Add("inStock", updated.InStock)
        .Build();

    var updateResult = await store.UpdateAsync(
        (UuidV7)skuLookup.Id.Value,
        updated,
        expectedEntityVersion: skuLookup.Version.Value,
        keys: [DataStorageKey.Create(new SkuKey(updated.Sku))],
        updateSearchFields,
        expiration: null, // keep existing expiration
        outboxEvents: [],
        CancellationToken.None);

    Console.WriteLine(updateResult == UpdateResult.Success
        ? $"  Updated {updated.Name}: ${original.Price} → ${updated.Price}"
        : $"  Update failed: {updateResult}");
}

Console.WriteLine();

// ──────────────────────────────────────────────────────────────
// 8. Count entities
// ──────────────────────────────────────────────────────────────

var totalCount = await store.CountAsync(
    ProductDso.DsoVersion.EntityType,
    filter: null,
    CancellationToken.None);

var inStockCount = await store.CountAsync(
    ProductDso.DsoVersion.EntityType,
    inStockField.IsTrue(),
    CancellationToken.None);

Console.WriteLine($"── Counts: {totalCount} total, {inStockCount} in stock ──");
Console.WriteLine();

// ──────────────────────────────────────────────────────────────
// 9. Delete by key
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Delete by SKU key ──");
var deleteResult = await store.DeleteAsync(
    ProductDso.DsoVersion.EntityType,
    DataStorageKey.Create(new SkuKey("SKU-003")),
    outboxEvents: [],
    CancellationToken.None);

Console.WriteLine($"  Deleted SKU-003: {deleteResult}");

var remainingCount = await store.CountAsync(
    ProductDso.DsoVersion.EntityType,
    filter: null,
    CancellationToken.None);

Console.WriteLine($"  Remaining products: {remainingCount}");
Console.WriteLine();
Console.WriteLine("Done!");

// ══════════════════════════════════════════════════════════════
// Custom entity and key definitions
// ══════════════════════════════════════════════════════════════

/// <summary>
/// A custom Data Storage Object (DSO) representing a product.
/// Each DSO type needs a unique EntityType ID and a schema version.
/// </summary>
internal sealed record ProductDso(
    string Name,
    string Sku,
    decimal Price,
    bool InStock) : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } =
        new(new EntityType(100, nameof(ProductDso)), SchemaVersion: 1);
}

/// <summary>
/// An alternate key for looking up products by SKU.
/// Implements IDataStorageKey for JSON-serialized composite keys.
/// </summary>
internal sealed record SkuKey(string Sku) : IDataStorageKey
{
    public static DataStorageKeyVersion DskVersion { get; } =
        new(new DataStorageKeyType(100, nameof(SkuKey)), SchemaVersion: 1);
}
