// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Import;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal.Storage;

/// <summary>
/// Creates a <see cref="TestUser" /> in User Management instead of as an IdentityServer test user (D20, D9). The user
/// keeps the harness-generated subject ID, so it is recognizable across stores and snapshots the same way a test user
/// is. Its profile holds only the <c>name</c> attribute, set to the test user's username, and the <c>email</c>
/// attribute the schema requires (synthesized from the username): not requested by the client's scopes in this
/// slice's scenarios, so it never appears in a snapshot.
/// </summary>
internal static class UserManagementProvisioning
{
    /// <summary>
    /// The attribute User Management looks the user up by at sign-in: a synthetic address, not shown to the test or
    /// the user, built from the username so sign-in needs nothing the harness didn't already generate.
    /// </summary>
    public static EmailAddress LoginAddressFor(string username) => EmailAddress.Create($"{username}@outside-in.test");

    public static async Task AddUserAsync(IServiceProvider services, TestUser user, Ct cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(user);

        var profileAdmin = services.GetRequiredService<IUserProfileAdmin>();
        var hashAlgorithm = services.GetRequiredService<IPasswordHashAlgorithm>();
        var importer = services.GetRequiredService<IUserImporter>();

        var schema = await profileAdmin.GetSchemaAsync(cancellationToken);
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(OidcStandardAttributes.Email.Code, LoginAddressFor(user.Username).Value);
        attributes.Set(OidcStandardAttributes.Name.Code, user.Username);

        var batch = await importer.ImportAsync(
            [
                new UserImportRecord
                {
                    SubjectId = UserSubjectId.Create(user.SubjectId),
                    ProfileAttributes = attributes.Validate(),
                    Authenticators = new AuthenticatorImport { Password = new PasswordImport(hashAlgorithm.Hash(user.Password)) },
                },
            ],
            cancellationToken);

        var result = batch.Results[0];
        if (result.Status is not (UserImportStatus.Created or UserImportStatus.Updated))
        {
            throw new InvalidOperationException($"User Management rejected user {user.Username}: {result.Error}");
        }
    }
}
