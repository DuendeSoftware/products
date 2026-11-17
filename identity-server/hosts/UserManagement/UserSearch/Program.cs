// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.DataExtensions;
using Duende.IdentityServer.Models;
using Duende.Storage.PostgreSql;
using Duende.Storage.Schema;
using Duende.UserManagement;
using Duende.UserManagement.Authentication;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using StorageInstanceId = Duende.Storage.StorageInstanceId;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.AddSingleton(new NpgsqlDataSourceBuilder(config.GetConnectionString("pgsql")!).Build());

services.AddLogging();

var identityServerBuilder = services
    .AddIdentityServer()
    .AddStorage(storage => storage.AddPostgreSql())
    // This sample creates its profile schema at startup through ISchemaAdmin.
    .AddDynamicSchemas();

identityServerBuilder
    .AddUserManagement(StorageInstanceId.Default, c => { });

await using var sp = services.BuildServiceProvider();
var ct = Ct.None;

await sp.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(ct);

var schemaAdmin = sp.GetRequiredService<ISchemaAdmin>();
var userAdmin = sp.GetRequiredService<IUserAdmin>();

await SeedDataAsync(schemaAdmin, userAdmin, ct);

await ListAllUsersAsync(userAdmin.Profiles, ct);
await FilterByExactNameAsync(userAdmin.Profiles, ct);
await FilterByEmailContainsAsync(userAdmin.Profiles, ct);
await SortByNameAsync(userAdmin.Profiles, ct);
await PaginateAsync(userAdmin.Profiles, ct);
await ProjectAttributesAsync(userAdmin.Profiles, ct);
await QueryAuthenticatorsAsync(userAdmin.Authenticators, ct);

return;

static async Task SeedDataAsync(
    ISchemaAdmin schemaAdmin,
    IUserAdmin userAdmin,
    Ct ct)
{
    var getResult = await schemaAdmin.GetAsync(SchemaId.UserProfile, ct);
    var schema = getResult.Found
        ? getResult.Item!
        : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };

    schema.AttributeDefinitions.Add(
        new AttributeDefinition { Code = Names.Name, AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("Display name") });
    schema.AttributeDefinitions.Add(
        new AttributeDefinition { Code = Names.Email, AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("Email address") });
    schema.AttributeDefinitions.Add(
        new AttributeDefinition { Code = Names.Role, AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("User role") });

    if (getResult.Found)
    {
        _ = await schemaAdmin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, ct);
    }
    else
    {
        _ = await schemaAdmin.CreateAsync(schema, ct);
    }

    var profileSchema = await userAdmin.Profiles.GetSchemaAsync(ct);

    (string Name, string Email, string Role)[] users =
    [
        ("Alice", "alice@example.com", "admin"),
        ("Bob", "bob@example.com", "user"),
        ("Charlie", "charlie@example.com", "user"),
        ("Diana", "diana@acme.com", "admin"),
        ("Eve", "eve@acme.com", "user"),
        ("Frank", "frank@example.com", "moderator"),
        ("Grace", "grace@acme.com", "user"),
    ];

    foreach (var (name, email, role) in users)
    {
        var attributes = new AttributeValueCollection(profileSchema);
        attributes.Set(Names.Name, name);
        attributes.Set(Names.Email, email);
        attributes.Set(Names.Role, role);

        var profile = await userAdmin.Profiles.TryAddAsync(UserSubjectId.New(), attributes.Validate(), ct);
        if (profile is null)
        {
            continue;
        }

        var otpAddress = new OtpAddress(OtpChannel.Email, EmailAddress.Create(email));
        _ = await userAdmin.Authenticators.TryAddAsync(profile.SubjectId, [otpAddress], [], ct);
    }
}

// --- Query scenarios ---

// Query all users with no filter, sort, or paging.
static async Task ListAllUsersAsync(IUserProfileAdmin admin, Ct ct)
{
    var request = QueryRequest.Empty;
    _ = await admin.QueryAsync(request, ct);
}

// Filter using a SCIM "eq" expression on the name attribute.
static async Task FilterByExactNameAsync(IUserProfileAdmin admin, Ct ct)
{
    var filter = FilterBy.FromSearchExpression(
        SearchExpression.Create("name eq \"Alice\""));
    var request = QueryRequest.Create(filter);
    _ = await admin.QueryAsync(request, ct);
}

// Filter using a SCIM "co" (contains) expression on email.
static async Task FilterByEmailContainsAsync(IUserProfileAdmin admin, Ct ct)
{
    var filter = FilterBy.FromSearchExpression(
        SearchExpression.Create("email co \"example.com\""));
    var request = QueryRequest.Create(filter);
    _ = await admin.QueryAsync(request, ct);
}

// Sort results by the name attribute in ascending order.
static async Task SortByNameAsync(IUserProfileAdmin admin, Ct ct)
{
    var sort = SortBy.Attribute(Names.Name);
    var request = QueryRequest.Create(sort);
    _ = await admin.QueryAsync(request, ct);
}

// Page through results using page-based pagination.
static async Task PaginateAsync(IUserProfileAdmin admin, Ct ct)
{
    var request = QueryRequest.Create(DataRange.FromPage(1, 3));
    _ = await admin.QueryAsync(request, ct);

    request = QueryRequest.Create(DataRange.FromPage(2, 3));
    _ = await admin.QueryAsync(request, ct);
}

// Request only specific attributes via the projection overload.
static async Task ProjectAttributesAsync(IUserProfileAdmin admin, Ct ct)
{
    var request = QueryRequest.Empty;
    var result = await admin.QueryAsync(request, [Names.Name, Names.Email], ct);

    foreach (var user in result.Results.Take(3))
    {
        _ = user.TryGet(Names.Name, out var nameValue);
        _ = user.TryGet(Names.Email, out var emailValue);
    }
}

// Query authenticator records with pagination.
static async Task QueryAuthenticatorsAsync(IUserAuthenticatorsAdmin admin, Ct ct)
{
    var request = QueryRequest.Create(DataRange.FromPage(1, 5));
    _ = await admin.QueryAsync(request, ct);
}

internal static class Names
{
    internal static readonly AttributeCode Name = AttributeCode.Create("name");
    internal static readonly AttributeCode Email = AttributeCode.Create("email");
    internal static readonly AttributeCode Role = AttributeCode.Create("role");
}
