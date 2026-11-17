// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.Storage;
using Duende.IdentityServer.DataExtensions;
using Duende.Storage.Schema;
using Duende.Storage.Sqlite;
using Duende.UserManagement;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Scim;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using ScimDev;

var builder = WebApplication.CreateBuilder(args);

var bearerToken = builder.Configuration["ScimDev:BearerToken"];
if (string.IsNullOrWhiteSpace(bearerToken))
{
    throw new InvalidOperationException(
        "Configure the reusable scim.dev token through ScimDev:BearerToken before starting the host.");
}

// VS Code terminates public HTTPS and forwards traffic to the loopback HTTP listener.
// Applying these headers lets SCIM responses advertise the public tunnel origin.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedHost | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
});

builder.Services.AddSingleton(new ScimDevCredential(bearerToken));

_ = builder.Services
    .AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, ScimDevAuthenticationHandler>(
        ScimDevAuthenticationDefaults.Scheme,
        _ => { });

_ = builder.Services.AddAuthorizationBuilder()
    .AddPolicy(ScimDevAuthenticationDefaults.Policy, policy => policy
        .AddAuthenticationSchemes(ScimDevAuthenticationDefaults.Scheme)
        .RequireAuthenticatedUser());

_ = builder.Services
    .AddIdentityServer()
    .AddStorage(storage =>
        storage.AddSqlite(options =>
            options.ConnectionString = "Data Source=ScimDev;Mode=Memory;Cache=Shared"))
    .AddUserManagement(StorageInstanceId.Default, users =>
{

    _ = users.EnableScim(
        options =>
        {
            options.EnableUsers = true;
            options.EnableGroups = true;

            // Microsoft's SCIM validator cannot synthesize test data for a
            // write-only "password" attribute and throws instead of skipping
            // it. Disable schema advertisement of changePassword here so the
            // validator's test data generator does not choke on it.
            // See README.md "Known limitations" for details.
            options.ChangePassword = false;
        },
        _ => { });

    _ = users.ConfigureScimOAuth(options =>
        options.AuthorizationPolicyName = ScimDevAuthenticationDefaults.Policy);
})
    // This sample creates its profile schema at startup through ISchemaAdmin.
    .AddDynamicSchemas();

var app = builder.Build();

// Prepare a fresh disposable store before accepting requests.
await using (var scope = app.Services.CreateAsyncScope())
{
    var cancellationToken = app.Lifetime.ApplicationStopping;
    await scope.ServiceProvider.GetRequiredService<IStorageInstanceSchema>().MigrateAsync(cancellationToken);

    var schema = new SchemaConfiguration
    {
        SchemaId = SchemaId.UserProfile,
        Groups = [.. DefaultScimUserSchema.Groups],
        AttributeDefinitions = [.. DefaultScimUserSchema.AttributeDefinitions]
    };

    var result = await scope.ServiceProvider
        .GetRequiredService<ISchemaAdmin>()
        .CreateAsync(schema, cancellationToken);

    if (!result.IsSuccess)
    {
        throw new InvalidOperationException("Failed to create the default SCIM user schema.");
    }

    // Stable records support read, filter, sort, and pagination checks.
    var profileAdmin = scope.ServiceProvider.GetRequiredService<IUserProfileAdmin>();
    var profileSchema = await profileAdmin.GetSchemaAsync(cancellationToken);

    (string UserName, string DisplayName)[] users =
    [
        ("alice@example.com", "Alice Example"),
        ("bob@example.com", "Bob Example")
    ];

    foreach (var (UserName, DisplayName) in users)
    {
        var attributes = new AttributeValueCollection(profileSchema);
        attributes.Set(DefaultScimUserSchema.UserName.Code, UserName);
        attributes.Set(DefaultScimUserSchema.DisplayName.Code, DisplayName);
        attributes.Set(DefaultScimUserSchema.Active.Code, true);

        _ = await profileAdmin.TryAddAsync(
            UserSubjectId.New(),
            attributes.Validate(),
            cancellationToken) ??
            throw new InvalidOperationException($"Failed to create seeded user '{UserName}'.");
    }
}

// Forwarded headers must run before SCIM generates absolute resource URLs.
app.UseForwardedHeaders();
app.UseAuthentication();
app.UseAuthorization();

_ = app.MapScim();

await app.RunAsync();
