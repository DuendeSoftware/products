// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Fields;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

// ──────────────────────────────────────────────────────────────
// EAV Schema Sample
//
// Demonstrates the Entity-Attribute-Value pattern:
// 1. Define a schema with typed attributes (scalars, complex, lists)
// 2. Validate attribute values against the schema at creation time
// 3. Store entities with flexible, schema-validated properties
// 4. Query using denormalized search fields
// ──────────────────────────────────────────────────────────────

// ── Setup ──

var services = new ServiceCollection();
services.AddLogging();
services.AddStorageInternal(storage => storage.AddSqliteStore(options =>
{
    options.ConnectionString = "Data Source=eav-sample.db";
}));

services.AddDsoRegistration<ContactDso>();

var provider = services.BuildServiceProvider();
var pooledStore = provider.GetRequiredService<IPooledStore>();
await pooledStore.MigrateAsync(CancellationToken.None);
var store = pooledStore.OpenPool(1);

Console.WriteLine("✓ Store initialized\n");

// ──────────────────────────────────────────────────────────────
// 1. Define an attribute schema
//
// The schema defines what attributes are allowed, their types,
// and validation rules. Think of it as a dynamic "class" definition.
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Defining attribute schema ──");

var schema = AttributeSchema.Load([], []);

// Simple scalar attributes
schema.AddAttributeDefinition(new AttributeDefinition
{
    Code = AttributeCode.Create("first_name"),
    AttributeType = new ScalarAttributeType(ScalarDataType.String),
    Description = AttributeDescription.Create("First name")
});

schema.AddAttributeDefinition(new AttributeDefinition
{
    Code = AttributeCode.Create("last_name"),
    AttributeType = new ScalarAttributeType(ScalarDataType.String),
    Description = AttributeDescription.Create("Last name")
});

schema.AddAttributeDefinition(new AttributeDefinition
{
    Code = AttributeCode.Create("age"),
    AttributeType = new ScalarAttributeType(ScalarDataType.Integer),
    Description = AttributeDescription.Create("Age in years")
});

schema.AddAttributeDefinition(new AttributeDefinition
{
    Code = AttributeCode.Create("active"),
    AttributeType = new ScalarAttributeType(ScalarDataType.Boolean),
    Description = AttributeDescription.Create("Whether the contact is active")
});

schema.AddAttributeDefinition(new AttributeDefinition
{
    Code = AttributeCode.Create("signup_date"),
    AttributeType = new ScalarAttributeType(ScalarDataType.Date),
    Description = AttributeDescription.Create("Date the contact signed up")
});

// Complex attribute — an address with nested fields
var addressType = new ComplexAttributeType(
    new Dictionary<AttributeCode, ComplexAttributeProperty>
    {
        [AttributeCode.Create("street")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("city")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("zip")] = ComplexAttributeProperty.Of(ScalarDataType.String),
    });

schema.AddAttributeDefinition(new AttributeDefinition
{
    Code = AttributeCode.Create("address"),
    AttributeType = addressType,
    Description = AttributeDescription.Create("Mailing address")
});

// List attribute — multiple phone numbers (list of complex objects)
var phoneListType = new ListAttributeType(
    new ComplexAttributeType(new Dictionary<AttributeCode, ComplexAttributeProperty>
    {
        [AttributeCode.Create("number")] = ComplexAttributeProperty.Of(ScalarDataType.String),
        [AttributeCode.Create("type")] = ComplexAttributeProperty.Of(ScalarDataType.String),
    }));

schema.AddAttributeDefinition(new AttributeDefinition
{
    Code = AttributeCode.Create("phones"),
    AttributeType = phoneListType,
    Description = AttributeDescription.Create("Phone numbers")
});

// Simple list — tags as a list of strings
var tagsType = new ListAttributeType(new ScalarAttributeType(ScalarDataType.String));

schema.AddAttributeDefinition(new AttributeDefinition
{
    Code = AttributeCode.Create("tags"),
    AttributeType = tagsType,
    Description = AttributeDescription.Create("Tags for categorization")
});

Console.WriteLine("  Defined 8 attributes: first_name, last_name, age, active, signup_date, address, phones, tags\n");

// ──────────────────────────────────────────────────────────────
// 2. Validate attribute values using the schema
//
// The schema validates values at creation time — type mismatches
// are caught immediately, not at storage time.
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Schema validation examples ──");

// Valid: string value for a string attribute
var collection = new AttributeValueCollection(schema);
collection.Set(AttributeCode.Create("first_name"), "Alice");
var nameAttr = collection[AttributeCode.Create("first_name")];
Console.WriteLine($"  ✓ Created: {nameAttr.Code} = {nameAttr.UntypedValue}");

// Valid: integer value
collection.Set(AttributeCode.Create("age"), 32);
var ageAttr = collection[AttributeCode.Create("age")];
Console.WriteLine($"  ✓ Created: {ageAttr.Code} = {ageAttr.UntypedValue}");

// Invalid: trying to create an attribute not in the schema
var invalidResult = collection.TrySet(AttributeCode.Create("unknown_field"), "oops", out _);
Console.WriteLine($"  ✗ Unknown attribute: {(invalidResult ? "accepted" : "rejected")}");

// Invalid: wrong type (string value for integer attribute)
var wrongTypeResult = collection.TrySet(AttributeCode.Create("age"), "not a number", out _);
Console.WriteLine($"  ✗ Wrong type (string for int): {(wrongTypeResult ? "accepted" : "rejected")}");

Console.WriteLine();

// ──────────────────────────────────────────────────────────────
// 3. Create contacts and store them
//
// The DSO stores attributes as a serializable dictionary.
// Search fields are populated for efficient querying.
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Creating contacts ──\n");

var contacts = new[]
{
    CreateContact(schema, "Alice", "Smith", 32, true, "Portland",
        phones: [("555-0101", "mobile"), ("555-0102", "work")],
        tags: ["vip", "engineering"]),

    CreateContact(schema, "Bob", "Jones", 28, true, "Seattle",
        phones: [("555-0201", "mobile")],
        tags: ["sales"]),

    CreateContact(schema, "Carol", "Davis", 45, false, "Portland",
        phones: [("555-0301", "home"), ("555-0302", "mobile")],
        tags: ["engineering", "management"]),
};

foreach (var (name, dso, searchFields) in contacts)
{
    var id = UuidV7.New();
    var result = await store.CreateAsync(
        id, dso, [], searchFields, Expiration.NoExpiration, [], CancellationToken.None);

    Console.WriteLine(result == CreateResult.Success
        ? $"  Created: {name}"
        : $"  Skipped: {name}");
}

Console.WriteLine();

// ──────────────────────────────────────────────────────────────
// 4. Query using search fields
//
// Search fields are the indexed projections of your EAV data.
// They enable efficient filtering without deserializing entities.
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Query: active contacts in Portland ──");

var activeField = new BooleanField("active");
var cityField = new StringField("city");
var filter = new AndExpression(
    activeField.IsTrue(),
    cityField.Equals("Portland"));

var results = await store.QueryAsync<ContactDso>(
    ContactDso.DsoVersion.EntityType,
    filter,
    SortParameter.Empty,
    PagedDataRange.First((DataRangeSize)10),
    CancellationToken.None);

foreach (var envelope in results)
{
    var c = envelope.Value;
    Console.WriteLine($"  {c.FirstName} {c.LastName} (city: {c.City})");
}

Console.WriteLine();

// ──────────────────────────────────────────────────────────────
// 5. Query: contacts with "engineering" tag
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Query: contacts tagged 'engineering' ──");

var tagsField = new StringArrayField("tags");
var tagFilter = tagsField.Contains("engineering");

var tagResults = await store.QueryAsync<ContactDso>(
    ContactDso.DsoVersion.EntityType,
    tagFilter,
    SortParameter.Empty,
    PagedDataRange.First((DataRangeSize)10),
    CancellationToken.None);

foreach (var envelope in tagResults)
{
    var c = envelope.Value;
    Console.WriteLine($"  {c.FirstName} {c.LastName} (active: {c.Active})");
}

Console.WriteLine();

// ──────────────────────────────────────────────────────────────
// 6. Query: age range filtering
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Query: contacts aged 30-50 ──");

var ageField = new NumberField("age");
var ageFilter = ageField.Between(30, 50);

var ageResults = await store.QueryAsync<ContactDso>(
    ContactDso.DsoVersion.EntityType,
    ageFilter,
    new SortParameter(ageField, SortDirection.Ascending),
    PagedDataRange.First((DataRangeSize)10),
    CancellationToken.None);

foreach (var envelope in ageResults)
{
    var c = envelope.Value;
    Console.WriteLine($"  {c.FirstName} {c.LastName} (age: {c.Age})");
}

Console.WriteLine();

// ──────────────────────────────────────────────────────────────
// 7. Read back and inspect the stored data
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Read: all contacts ──");

var allContacts = await store.QueryAsync<ContactDso>(
    ContactDso.DsoVersion.EntityType,
    Query.All(),
    new SortParameter(new StringField("last_name"), SortDirection.Ascending),
    PagedDataRange.First((DataRangeSize)10),
    CancellationToken.None);

foreach (var envelope in allContacts)
{
    var c = envelope.Value;
    var phoneSummary = c.Phones != null ? string.Join(", ", c.Phones.Select(p => $"{p.Type}:{p.Number}")) : "";
    var tagSummary = c.Tags != null ? string.Join(", ", c.Tags) : "";
    Console.WriteLine($"  {c.FirstName} {c.LastName} | age:{c.Age} | city:{c.City} | phones:[{phoneSummary}] | tags:[{tagSummary}]");
}

Console.WriteLine("\nDone!");

// ══════════════════════════════════════════════════════════════
// Helper methods
// ══════════════════════════════════════════════════════════════

static (string Name, ContactDso Dso, SearchFieldCollection SearchFields) CreateContact(
    AttributeSchema schema,
    string firstName, string lastName, int age, bool active, string city,
    (string Number, string Type)[] phones,
    string[] tags)
{
    // Validate all values against the schema before storing
    var validationCol = new AttributeValueCollection(schema);
    validationCol.Set(AttributeCode.Create("first_name"), firstName);
    validationCol.Set(AttributeCode.Create("last_name"), lastName);
    validationCol.Set(AttributeCode.Create("age"), age);
    validationCol.Set(AttributeCode.Create("active"), active);
    validationCol.Set(AttributeCode.Create("signup_date"), DateOnly.FromDateTime(DateTime.Today));

    // Complex attribute validation
    var address = new Dictionary<string, object> { ["street"] = "123 Main St", ["city"] = city, ["zip"] = "00000" };
    validationCol.Set(AttributeCode.Create("address"), (IReadOnlyDictionary<string, object>)address);

    // List attribute validation
    var phoneList = phones.Select(p => (object)new Dictionary<string, object> { ["number"] = p.Number, ["type"] = p.Type }).ToList();
    validationCol.Set(AttributeCode.Create("phones"), (IReadOnlyList<object>)phoneList);
    validationCol.Set(AttributeCode.Create("tags"), (IReadOnlyList<object>)tags.Cast<object>().ToList());

    // Store as a simple record (JSON-serializable)
    var dso = new ContactDso(
        firstName, lastName, age, active, city,
        phones.Select(p => new PhoneEntry(p.Number, p.Type)).ToArray(),
        tags);

    // Build search fields for efficient querying
    var builder = new SearchFieldsBuilder()
        .Add("first_name", firstName)
        .Add("last_name", lastName)
        .Add("age", (decimal)age)
        .Add("active", active)
        .Add("city", city);

    // Index array items individually for array-contains queries
    for (var i = 0; i < tags.Length; i++)
    {
        _ = builder.Add("tags", i, tags[i]);
    }

    return ($"{firstName} {lastName}", dso, builder.Build());
}

// ══════════════════════════════════════════════════════════════
// Entity definitions
// ══════════════════════════════════════════════════════════════

/// <summary>
/// A DSO representing a contact. The schema validates attribute values
/// before storage, but the stored form is a simple JSON-serializable record.
/// </summary>
internal sealed record ContactDso(
    string FirstName,
    string LastName,
    int Age,
    bool Active,
    string City,
    PhoneEntry[] Phones,
    string[] Tags) : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } =
        new(new EntityType(300, nameof(ContactDso)), SchemaVersion: 1);
}

internal sealed record PhoneEntry(string Number, string Type);
