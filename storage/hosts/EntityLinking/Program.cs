// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.Internal;
using Duende.Storage.Internal.Builder;
using Duende.Storage.Internal.Querying;
using Duende.Storage.Internal.Querying.SearchFields;
using Duende.Storage.Pagination;
using Duende.Storage.Sqlite;
using Microsoft.Extensions.DependencyInjection;

// ──────────────────────────────────────────────────────────────
// Entity Linking Sample
//
// Demonstrates how to create relationships (links) between
// entities and query across them — like a lightweight graph.
// ──────────────────────────────────────────────────────────────

// ── Setup ──

var services = new ServiceCollection();
services.AddLogging();
services.AddStorageInternal(storage => storage.AddSqliteStore(options =>
{
    options.ConnectionString = "Data Source=linking-sample.db";
}));

services.AddDsoRegistration<TeamDso>();
services.AddDsoRegistration<DeveloperDso>();
services.AddDsoRegistration<ProjectDso>();

var provider = services.BuildServiceProvider();
var pooledStore = provider.GetRequiredService<IPooledStore>();
await pooledStore.MigrateAsync(CancellationToken.None);
var store = pooledStore.OpenPool(1);

Console.WriteLine("✓ Store initialized\n");

// ──────────────────────────────────────────────────────────────
// 1. Define link types — these describe the relationships
// ──────────────────────────────────────────────────────────────

// Link definitions describe which entity types can be connected and how
var teamMember = new LinkDefinition
{
    Left = TeamDso.DsoVersion.EntityType,       // Team
    Right = DeveloperDso.DsoVersion.EntityType,  // Developer
    Link = new LinkType(200, "HasMember")
};

var projectAssignment = new LinkDefinition
{
    Left = DeveloperDso.DsoVersion.EntityType,  // Developer
    Right = ProjectDso.DsoVersion.EntityType,    // Project
    Link = new LinkType(201, "AssignedTo")
};

// ──────────────────────────────────────────────────────────────
// 2. Create some entities
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Creating entities ──");

var platformTeamId = UuidV7.New();
var mobileTeamId = UuidV7.New();

var aliceId = UuidV7.New();
var bobId = UuidV7.New();
var charlieId = UuidV7.New();

var authProjectId = UuidV7.New();
var dashboardProjectId = UuidV7.New();
var sdkProjectId = UuidV7.New();

// Teams
await store.CreateAsync(platformTeamId, new TeamDso("Platform"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], CancellationToken.None);
await store.CreateAsync(mobileTeamId, new TeamDso("Mobile"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], CancellationToken.None);

// Developers
await store.CreateAsync(aliceId, new DeveloperDso("Alice", "Senior"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], CancellationToken.None);
await store.CreateAsync(bobId, new DeveloperDso("Bob", "Mid"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], CancellationToken.None);
await store.CreateAsync(charlieId, new DeveloperDso("Charlie", "Junior"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], CancellationToken.None);

// Projects
await store.CreateAsync(authProjectId, new ProjectDso("Auth Service"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], CancellationToken.None);
await store.CreateAsync(dashboardProjectId, new ProjectDso("Dashboard"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], CancellationToken.None);
await store.CreateAsync(sdkProjectId, new ProjectDso("Mobile SDK"), [], SearchFieldCollection.Empty, Expiration.NoExpiration, [], CancellationToken.None);

Console.WriteLine("  Created 2 teams, 3 developers, 3 projects\n");

// ──────────────────────────────────────────────────────────────
// 3. Create links between entities
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Creating links ──");

// Platform team members: Alice, Bob
await store.LinkAsync(teamMember, platformTeamId, aliceId, [], CancellationToken.None);
await store.LinkAsync(teamMember, platformTeamId, bobId, [], CancellationToken.None);

// Mobile team members: Charlie, Alice (Alice is on both teams!)
await store.LinkAsync(teamMember, mobileTeamId, charlieId, [], CancellationToken.None);
await store.LinkAsync(teamMember, mobileTeamId, aliceId, [], CancellationToken.None);

// Project assignments
await store.LinkAsync(projectAssignment, aliceId, authProjectId, [], CancellationToken.None);
await store.LinkAsync(projectAssignment, aliceId, sdkProjectId, [], CancellationToken.None);
await store.LinkAsync(projectAssignment, bobId, dashboardProjectId, [], CancellationToken.None);
await store.LinkAsync(projectAssignment, charlieId, sdkProjectId, [], CancellationToken.None);

// Demonstrate idempotent linking — linking again returns AlreadyLinked
var duplicateResult = await store.LinkAsync(teamMember, platformTeamId, aliceId, [], CancellationToken.None);
Console.WriteLine($"  Duplicate link result: {duplicateResult}");

Console.WriteLine("  Platform team → Alice, Bob");
Console.WriteLine("  Mobile team → Charlie, Alice");
Console.WriteLine("  Alice → Auth Service, Mobile SDK");
Console.WriteLine("  Bob → Dashboard");
Console.WriteLine("  Charlie → Mobile SDK\n");

// ──────────────────────────────────────────────────────────────
// 4. Query: "Who is on the Platform team?"
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Query: Members of Platform team ──");

var membersQuery = LinkQuery
    .From(DeveloperDso.DsoVersion.EntityType)  // We want developers
    .Join(teamMember)                           // Connected via teamMember link
    .Where(TeamDso.DsoVersion.EntityType, platformTeamId)  // Starting from Platform team
    .Build();

var members = await store.QueryLinksAsync<DeveloperDso>(
    membersQuery,
    PagedDataRange.First((DataRangeSize)10),
    CancellationToken.None);

foreach (var m in members)
{
    Console.WriteLine($"  {m.Value.Name} ({m.Value.Level})");
}

Console.WriteLine();

// ──────────────────────────────────────────────────────────────
// 5. Query: "What projects is Alice assigned to?"
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Query: Alice's projects ──");

var aliceProjectsQuery = LinkQuery
    .From(ProjectDso.DsoVersion.EntityType)
    .Join(projectAssignment)
    .Where(DeveloperDso.DsoVersion.EntityType, aliceId)
    .Build();

var aliceProjects = await store.QueryLinksAsync<ProjectDso>(
    aliceProjectsQuery,
    PagedDataRange.First((DataRangeSize)10),
    CancellationToken.None);

foreach (var p in aliceProjects)
{
    Console.WriteLine($"  {p.Value.Name}");
}

Console.WriteLine();

// ──────────────────────────────────────────────────────────────
// 6. Multi-hop query: "What projects does the Platform team work on?"
//    (Team → Developer → Project, two hops)
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Multi-hop: Platform team's projects ──");

var teamProjectsQuery = LinkQuery
    .From(ProjectDso.DsoVersion.EntityType)   // We want projects
    .Join(projectAssignment)                    // Developer → Project
    .Join(teamMember)                           // Team → Developer
    .Where(TeamDso.DsoVersion.EntityType, platformTeamId)
    .Build();

var teamProjects = await store.QueryLinksAsync<ProjectDso>(
    teamProjectsQuery,
    PagedDataRange.First((DataRangeSize)10),
    CancellationToken.None);

foreach (var p in teamProjects)
{
    Console.WriteLine($"  {p.Value.Name}");
}

Console.WriteLine();

// ──────────────────────────────────────────────────────────────
// 7. Remove a link (unlink)
// ──────────────────────────────────────────────────────────────

Console.WriteLine("── Unlink: Remove Alice from Mobile team ──");

await store.UnlinkAsync(teamMember, mobileTeamId, aliceId, [], CancellationToken.None);

// Verify — query Mobile team members again
var mobileQuery = LinkQuery
    .From(DeveloperDso.DsoVersion.EntityType)
    .Join(teamMember)
    .Where(TeamDso.DsoVersion.EntityType, mobileTeamId)
    .Build();

var mobileMembers = await store.QueryLinksAsync<DeveloperDso>(
    mobileQuery,
    PagedDataRange.First((DataRangeSize)10),
    CancellationToken.None);

Console.Write("  Mobile team now: ");
Console.WriteLine(string.Join(", ", mobileMembers.Select(m => m.Value.Name)));

Console.WriteLine("\nDone!");

// ══════════════════════════════════════════════════════════════
// Entity definitions
// ══════════════════════════════════════════════════════════════

internal sealed record TeamDso(string Name) : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } =
        new(new EntityType(200, nameof(TeamDso)), SchemaVersion: 1);
}

internal sealed record DeveloperDso(string Name, string Level) : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } =
        new(new EntityType(201, nameof(DeveloperDso)), SchemaVersion: 1);
}

internal sealed record ProjectDso(string Name) : IDataStorageObject
{
    public static DataStorageObjectVersion DsoVersion { get; } =
        new(new EntityType(202, nameof(ProjectDso)), SchemaVersion: 1);
}
