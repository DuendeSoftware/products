// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Platform.UserManagement.Import;
using Duende.IdentityServer.DataExtensions;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Import;
using Duende.UserManagement.Profiles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class ImportUsersHowToTests : IAsyncLifetime
{
    private readonly Ct _ct = TestContext.Current.CancellationToken;
    private ServiceProvider _serviceProvider = null!;
    private IUserImporter _import = null!;
    private IUserProfileAdmin _profileAdmin = null!;
    private IPasswordAuthenticator _auth = null!;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await CreateProviderWithAdapter();
        _import = _serviceProvider.GetRequiredService<IUserImporter>();
        _profileAdmin = _serviceProvider.GetRequiredService<IUserProfileAdmin>();
        _auth = _serviceProvider.GetRequiredService<IPasswordAuthenticator>();

        await RegisterEmailSchemaAsync(_serviceProvider.GetRequiredService<ISchemaAdmin>(), _ct);
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();

    private static async Task RegisterEmailSchemaAsync(ISchemaAdmin schemaAdmin, Ct ct)
    {
        var getResult = await schemaAdmin.GetAsync(SchemaId.UserProfile, ct);
        var schema = getResult.Found
            ? getResult.Item!
            : new SchemaConfiguration { SchemaId = SchemaId.UserProfile };

        schema.AttributeDefinitions.Add(OidcStandardAttributes.Email with { IsUnique = true });

        var saveResult = getResult.Found
            ? await schemaAdmin.UpdateAsync(SchemaId.UserProfile, schema, getResult.Version!.Value, ct)
            : await schemaAdmin.CreateAsync(schema, ct);

        saveResult.IsSuccess.ShouldBeTrue();
    }

    /// <summary>
    /// A host reads its ASP.NET Identity database itself (typically with EF Core) and produces this
    /// base64 hash blob per user. A real <c>PasswordHasher&lt;TUser&gt;.HashPassword</c> call stands in
    /// for that here.
    /// </summary>
    private static string HashWithAspNetIdentity(string password) =>
        new PasswordHasher<object>().HashPassword(new object(), password);

    private static async Task<ServiceProvider> CreateProviderWithAdapter(
        Guid? dbId = null, IUserImportConflictResolver? resolver = null) =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null,
            addDataProtection: true,
            configureServices: services =>
            {
                _ = services.AddSingleton<IPasswordHashAlgorithm, AspNetIdentityPasswordHashAlgorithm>();
                if (resolver is not null)
                {
                    _ = services.AddSingleton(resolver);
                }
            },
            dbId: dbId);

    private static async Task<ServiceProvider> CreateProviderWithPbkdf2Only(Guid? dbId = null) =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null,
            addDataProtection: true,
            dbId: dbId);

#pragma warning disable IDE0022 // Use expression body for method: a block body is required so a begin-snippet/end-snippet pair can wrap only the statements, not the signature.
    private static UserImportRecord BuildRecord(
        IReadOnlyAttributeSchema schema, UserSubjectId subjectId, string email, string aspNetPasswordHash)
    {
        // begin-snippet: import-build-record
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(AttributeCode.Create("email"), email);

        var record = new UserImportRecord
        {
            SubjectId = subjectId,
            ProfileAttributes = attributes.Validate(),
            Authenticators = new AuthenticatorImport
            {
                // Stored as-is. AspNetIdentityPasswordHashAlgorithm verifies it on sign-in.
                Password = new PasswordImport(AspNetIdentityPasswordHashAlgorithm.CreateImportData(aspNetPasswordHash))
            }
        };
        // end-snippet

        return record;
    }

    private static async Task<UserImportStatus> RunImport(IUserImporter importer, UserImportRecord record, Ct ct)
    {
        // begin-snippet: import-run
        var batch = await importer.ImportAsync([record], ct);
        var status = batch.Results.Single().Status;
        // end-snippet

        return status;
    }
#pragma warning restore IDE0022

    [Fact]
    public async Task Imported_hash_authenticates_with_the_original_password_and_rejects_a_wrong_one()
    {
        var subjectId = UserSubjectId.New();
        var email = $"frank-{Guid.NewGuid():N}@example.com";
        const string passwordText = "correct-horse-battery-staple-5";
        var aspNetHash = HashWithAspNetIdentity(passwordText);

        var schema = await _profileAdmin.GetSchemaAsync(_ct);
        var record = BuildRecord(schema, subjectId, email, aspNetHash);

        (await RunImport(_import, record, _ct)).ShouldBe(UserImportStatus.Created);

        var wrongResult = await _auth.TryAuthenticateAsync(
            AttributeCode.Create("email"), email, NonValidatedPassword.Create("wrong-horse-battery-staple-5"), _ct);
        _ = wrongResult.ShouldBeOfType<PasswordAuthenticationResult.Failure>();

        var correctResult = await _auth.TryAuthenticateAsync(
            AttributeCode.Create("email"), email, NonValidatedPassword.Create(passwordText), _ct);
        _ = correctResult.ShouldBeOfType<PasswordAuthenticationResult.Success>();
    }

    [Fact]
    public async Task Imported_hash_is_rehashed_to_pbkdf2_sha512_after_a_successful_sign_in()
    {
        var dbId = Guid.NewGuid();
        var subjectId = UserSubjectId.New();
        var email = $"frank-{Guid.NewGuid():N}@example.com";
        const string passwordText = "correct-horse-battery-staple-6";
        var aspNetHash = HashWithAspNetIdentity(passwordText);

        // Kept open for the duration of the test: the shared in-memory SQLite database needs at
        // least one open connection to survive across the ServiceProvider instances below.
        await using var seedProvider = await CreateProviderWithAdapter(dbId);
        await RegisterEmailSchemaAsync(seedProvider.GetRequiredService<ISchemaAdmin>(), _ct);

        var schema = await seedProvider.GetRequiredService<IUserProfileAdmin>().GetSchemaAsync(_ct);
        var record = BuildRecord(schema, subjectId, email, aspNetHash);
        (await RunImport(seedProvider.GetRequiredService<IUserImporter>(), record, _ct)).ShouldBe(UserImportStatus.Created);

        // Sign in with the adapter still registered: it verifies the imported hash and, because
        // AspNetIdentityPasswordHashAlgorithm.NeedsRehash always returns true, triggers a rehash
        // to the preferred algorithm (PBKDF2-SHA512 by default).
        await using (var authProvider = await CreateProviderWithAdapter(dbId))
        {
            var result = await authProvider.GetRequiredService<IPasswordAuthenticator>().TryAuthenticateAsync(
                AttributeCode.Create("email"), email, NonValidatedPassword.Create(passwordText), _ct);
            _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
        }

        // Verify: sign in again with only the default PBKDF2-SHA512 algorithm registered. The
        // adapter is no longer needed because the stored hash was rehashed above.
        await using (var verifyProvider = await CreateProviderWithPbkdf2Only(dbId))
        {
            var result = await verifyProvider.GetRequiredService<IPasswordAuthenticator>().TryAuthenticateAsync(
                AttributeCode.Create("email"), email, NonValidatedPassword.Create(passwordText), _ct);
            _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
        }
    }

    [Fact]
    public async Task Default_resolver_skips_a_duplicate_subject_on_re_import()
    {
        var subjectId = UserSubjectId.New();
        var record = new UserImportRecord { SubjectId = subjectId, ProfileAttributes = ValidatedAttributeValueCollection.Empty };

        (await RunImport(_import, record, _ct)).ShouldBe(UserImportStatus.Created);
        (await RunImport(_import, record, _ct)).ShouldBe(UserImportStatus.Skipped);
    }

    [Fact]
    public async Task Custom_resolver_overwrites_a_duplicate_subject_on_re_import()
    {
        await using var sp = await CreateProviderWithAdapter(resolver: new OverwriteOnDuplicateResolver());
        var import = sp.GetRequiredService<IUserImporter>();

        var subjectId = UserSubjectId.New();
        var record = new UserImportRecord { SubjectId = subjectId, ProfileAttributes = ValidatedAttributeValueCollection.Empty };

        (await RunImport(import, record, _ct)).ShouldBe(UserImportStatus.Created);
        (await RunImport(import, record, _ct)).ShouldBe(UserImportStatus.Updated);
    }

    [Fact]
    public async Task Custom_resolver_skips_a_unique_attribute_conflict_with_a_different_subject()
    {
        await using var sp = await CreateProviderWithAdapter(resolver: new OverwriteOnDuplicateResolver());
        var import = sp.GetRequiredService<IUserImporter>();
        var schemaAdmin = sp.GetRequiredService<ISchemaAdmin>();
        var profileAdmin = sp.GetRequiredService<IUserProfileAdmin>();
        await RegisterEmailSchemaAsync(schemaAdmin, _ct);

        var email = $"shared-{Guid.NewGuid():N}@example.com";
        var schema = await profileAdmin.GetSchemaAsync(_ct);

        var existingAttrs = new AttributeValueCollection(schema);
        existingAttrs.Set(AttributeCode.Create("email"), email);
        var existingRecord = new UserImportRecord { SubjectId = UserSubjectId.New(), ProfileAttributes = existingAttrs.Validate() };
        (await RunImport(import, existingRecord, _ct)).ShouldBe(UserImportStatus.Created);

        // A different subject importing the same (unique) email: OverwriteOnDuplicateResolver can't
        // safely overwrite here, because UserImportConflict doesn't say which existing subject holds
        // the conflicting email, so it skips instead (see "Resolve conflicts" below).
        var incomingAttrs = new AttributeValueCollection(schema);
        incomingAttrs.Set(AttributeCode.Create("email"), email);
        var incomingRecord = new UserImportRecord { SubjectId = UserSubjectId.New(), ProfileAttributes = incomingAttrs.Validate() };
        (await RunImport(import, incomingRecord, _ct)).ShouldBe(UserImportStatus.Skipped);
    }

    [Fact]
    public async Task Imported_hash_algorithm_id_matches_the_registered_adapter()
    {
        var aspNetHash = HashWithAspNetIdentity("correct-horse-battery-staple-7");
        var data = AspNetIdentityPasswordHashAlgorithm.CreateImportData(aspNetHash);

        data.AlgorithmId.ShouldBe(AspNetIdentityPasswordHashAlgorithm.Id);

        var algorithms = _serviceProvider.GetServices<IPasswordHashAlgorithm>();
        algorithms.ShouldContain(a => a.AlgorithmId == data.AlgorithmId);
    }

    [Fact]
    public void Verify_rejects_a_V3_hash_with_a_subkey_shorter_than_128_bits()
    {
        // 0x01 || PRF (SHA256=1) || iterations || salt length (16) || salt (16 bytes) || 1-byte subkey.
        var bytes = new List<byte> { 0x01 };
        bytes.AddRange(BitConverterBigEndian(1));
        bytes.AddRange(BitConverterBigEndian(1000));
        bytes.AddRange(BitConverterBigEndian(16));
        bytes.AddRange(new byte[16]);
        bytes.Add(0xFF); // 1-byte subkey: below the 128-bit (16-byte) minimum.
        var blob = Convert.ToBase64String(bytes.ToArray());

        var algorithm = new AspNetIdentityPasswordHashAlgorithm();
        var data = AspNetIdentityPasswordHashAlgorithm.CreateImportData(blob);

        algorithm.Verify("any-password", data).ShouldBeFalse();
    }

    [Fact]
    public void Verify_rejects_a_V3_hash_with_a_salt_shorter_than_128_bits()
    {
        // Salt length of 8 bytes: below the 128-bit (16-byte) minimum.
        var bytes = new List<byte> { 0x01 };
        bytes.AddRange(BitConverterBigEndian(1));
        bytes.AddRange(BitConverterBigEndian(1000));
        bytes.AddRange(BitConverterBigEndian(8));
        bytes.AddRange(new byte[8]);
        bytes.AddRange(new byte[32]);
        var blob = Convert.ToBase64String(bytes.ToArray());

        var algorithm = new AspNetIdentityPasswordHashAlgorithm();
        var data = AspNetIdentityPasswordHashAlgorithm.CreateImportData(blob);

        algorithm.Verify("any-password", data).ShouldBeFalse();
    }

    [Fact]
    public void Verify_rejects_a_truncated_V3_hash()
    {
        var blob = Convert.ToBase64String([0x01, 0x00, 0x00, 0x00]);

        var algorithm = new AspNetIdentityPasswordHashAlgorithm();
        var data = AspNetIdentityPasswordHashAlgorithm.CreateImportData(blob);

        algorithm.Verify("any-password", data).ShouldBeFalse();
    }

    private static byte[] BitConverterBigEndian(uint value)
    {
        var bytes = new byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }
}

// begin-snippet: import-custom-conflict-resolver
internal sealed class OverwriteOnDuplicateResolver : IUserImportConflictResolver
{
    public Task<UserImportConflictResolution> ResolveAsync(UserImportConflict conflict, Ct ct) =>
        Task.FromResult<UserImportConflictResolution>(conflict switch
        {
            { Reason: UserImportConflictReason.ConcurrencyConflict } => new UserImportConflictResolution.Retry(),

            // The incoming record's own SubjectId already exists: safe to overwrite that same user.
            { Step: UserImportStep.UserRecord } => new UserImportConflictResolution.Overwrite(conflict.Record.SubjectId),

            // A unique attribute (for example email) collides with a *different* existing user, and
            // UserImportConflict doesn't say which one. Overwrite(conflict.Record.SubjectId) would
            // target the wrong (nonexistent) profile and fail the record, so skip instead.
            _ => new UserImportConflictResolution.Skip()
        });
}
// end-snippet
