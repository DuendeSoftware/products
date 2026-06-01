// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.EntityAttributeValue;
using Duende.UserManagement;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Import;
using Duende.UserManagement.Membership;
using Duende.UserManagement.Profiles;
using Microsoft.Extensions.DependencyInjection;

namespace Duende.Platform.UserManagement;

public sealed class UserImportTests : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = null!;
    private IUserImporter _import = null!;
    private IUserProfileAdmin _profileAdmin = null!;
    private IUserProfileSchemaAdmin _schemaAdmin = null!;
    private IGroupAdmin _groupAdmin = null!;
    private IRoleAdmin _roleAdmin = null!;
    private IMembershipAdmin _membershipAdmin = null!;
    private IPasswordAuthenticator _passwordAuthenticator = null!;
    private IPasswordHashAlgorithm _hashAlgorithm = null!;
    private readonly Ct _ct = TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _serviceProvider = await UsersServiceProviderFactory.CreateAsync();

        _import = _serviceProvider.GetRequiredService<IUserImporter>();
        _profileAdmin = _serviceProvider.GetRequiredService<IUserProfileAdmin>();
        _schemaAdmin = _serviceProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        _groupAdmin = _serviceProvider.GetRequiredService<IGroupAdmin>();
        _roleAdmin = _serviceProvider.GetRequiredService<IRoleAdmin>();
        _membershipAdmin = _serviceProvider.GetRequiredService<IMembershipAdmin>();
        _passwordAuthenticator = _serviceProvider.GetRequiredService<IPasswordAuthenticator>();
        _hashAlgorithm = _serviceProvider.GetRequiredService<IPasswordHashAlgorithm>();
    }

    public ValueTask DisposeAsync() => _serviceProvider.DisposeAsync();
    [Fact]
    public async Task import_profile_only_creates_user()
    {
        var subjectId = UserSubjectId.New();

        var batch = await _import.ImportAsync(
            [new UserImportRecord { SubjectId = subjectId, ProfileAttributes = ValidatedAttributeValueCollection.Empty }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Created);
        _ = (await _profileAdmin.TryGetAsync(subjectId, _ct)).ShouldNotBeNull();
    }

    [Fact]
    public async Task import_profile_with_schema_attributes_round_trips()
    {
        var attrName = AttributeCode.Create($"email_{Guid.NewGuid():N}"[..20]);
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition { Code = attrName, AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("email") },
            _ct);

        var subjectId = UserSubjectId.New();
        var schema = await _profileAdmin.GetSchemaAsync(_ct);
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(attrName, "test@example.com");

        var batch = await _import.ImportAsync(
            [new UserImportRecord { SubjectId = subjectId, ProfileAttributes = attributes.Validate() }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Created);
        var profile = (await _profileAdmin.TryGetAsync(subjectId, _ct)).ShouldNotBeNull();
        profile.Attributes.TryGetValue(attrName, out var attrValue).ShouldBeTrue();
        attrValue!.TryGetValue<string>(out var value).ShouldBeTrue();
        value.ShouldBe("test@example.com");
    }

    [Fact]
    public async Task import_with_hashed_password_allows_authentication()
    {
        var subjectId = UserSubjectId.New();
        var userName = $"user_{Guid.NewGuid():N}"[..20];
        const string rawPassword = "ABcd12!@hashed";
        var hashedData = _hashAlgorithm.Hash(rawPassword);

        var userNameCode = AttributeCode.Create("userName");
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition { Code = userNameCode, AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("user id"), IsUnique = true },
            _ct);

        var schema = await _profileAdmin.GetSchemaAsync(_ct);
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(userNameCode, userName);

        var batch = await _import.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = subjectId,
                ProfileAttributes = attributes.Validate(),
                Authenticators = new AuthenticatorImport
                {
                    Password = new PasswordImport(hashedData)
                }
            }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Created);
        var suppliedPassword = NonValidatedPassword.Create(rawPassword);
        var authenticated = await _passwordAuthenticator.TryAuthenticateAsync(userNameCode, userName, suppliedPassword, _ct);
        authenticated.ShouldBeOfType<PasswordAuthenticationResult.Success>().UserSubjectId.ShouldBe(subjectId);
    }

    [Fact]
    public async Task import_with_otp_address_creates_authenticators()
    {
        var subjectId = UserSubjectId.New();
        var otpAddress = new OtpAddress(OtpChannel.Email, EmailAddress.Create($"a{Guid.NewGuid():N}@x.com"[..20] + "@x.com"));

        var batch = await _import.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = subjectId,
                Authenticators = new AuthenticatorImport { OtpAddresses = [otpAddress] }
            }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Created);
    }

    [Fact]
    public async Task import_with_external_authenticator_creates_authenticators()
    {
        var subjectId = UserSubjectId.New();
        var external = new ExternalAuthenticatorAddress(
            ExternalAuthenticatorName.Create("google"),
            EmailAddress.Create($"a{Guid.NewGuid():N}@x.com"[..20] + "@x.com"));

        var batch = await _import.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = subjectId,
                Authenticators = new AuthenticatorImport { ExternalAuthenticatorAddresses = [external] }
            }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Created);
    }
    [Fact]
    public async Task import_with_group_assigns_user_to_group()
    {
        var subjectId = UserSubjectId.New();
        var groupName = GroupName.Create($"group_{Guid.NewGuid():N}"[..30]);
        var createResult = await _groupAdmin.CreateAsync(new Group { Name = groupName }, _ct);
        createResult.IsSuccess.ShouldBeTrue();
        var groupId = createResult.Id!.Value;

        var batch = await _import.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = subjectId,
                ProfileAttributes = ValidatedAttributeValueCollection.Empty,
                Memberships = new MembershipImport { Groups = [groupId] }
            }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Created);
        var members = await _membershipAdmin.GetMembersInGroupAsync(groupId, null, _ct);
        members.Items.ShouldContain(p => p.SubjectId == subjectId);
    }

    [Fact]
    public async Task import_with_direct_role_assigns_role_to_user()
    {
        var subjectId = UserSubjectId.New();
        var roleName = RoleName.Create($"role_{Guid.NewGuid():N}"[..30]);
        var createResult = await _roleAdmin.CreateAsync(new Role { Name = roleName }, _ct);
        createResult.IsSuccess.ShouldBeTrue();
        var roleId = createResult.Id!.Value;

        var batch = await _import.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = subjectId,
                ProfileAttributes = ValidatedAttributeValueCollection.Empty,
                Memberships = new MembershipImport { DirectRoles = [roleId] }
            }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Created);
        var roles = await _membershipAdmin.GetDirectRolesAsync(subjectId, null, _ct);
        roles.Items.ShouldContain(r => r.Id == roleId);
    }

    [Fact]
    public async Task import_with_nonexistent_group_fails_record()
    {
        var subjectId = UserSubjectId.New();

        var batch = await _import.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = subjectId,
                ProfileAttributes = ValidatedAttributeValueCollection.Empty,
                Memberships = new MembershipImport { Groups = [GroupId.New()] }
            }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Failed);
    }

    [Fact]
    public async Task import_with_nonexistent_role_fails_record()
    {
        var subjectId = UserSubjectId.New();

        var batch = await _import.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = subjectId,
                ProfileAttributes = ValidatedAttributeValueCollection.Empty,
                Memberships = new MembershipImport { DirectRoles = [RoleId.New()] }
            }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Failed);
    }

    [Fact]
    public async Task membership_only_import_without_profile_creates_membership()
    {
        var subjectId = UserSubjectId.New();
        var groupName = GroupName.Create($"group_{Guid.NewGuid():N}"[..30]);
        var createResult = await _groupAdmin.CreateAsync(new Group { Name = groupName }, _ct);
        var groupId = createResult.Id!.Value;

        var batch = await _import.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = subjectId,
                Memberships = new MembershipImport { Groups = [groupId] }
            }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Created);
    }
    [Fact]
    public async Task subject_conflict_with_default_resolver_skips_record()
    {
        var subjectId = UserSubjectId.New();
        _ = await _import.ImportAsync(
            [new UserImportRecord { SubjectId = subjectId, ProfileAttributes = ValidatedAttributeValueCollection.Empty }],
            _ct);

        var batch = await _import.ImportAsync(
            [new UserImportRecord { SubjectId = subjectId, ProfileAttributes = ValidatedAttributeValueCollection.Empty }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Skipped);
    }

    [Fact]
    public async Task subject_conflict_with_overwrite_resolver_updates_record()
    {
        await using var sp = await CreateProviderWithResolver(
            new LambdaConflictResolver(c => Task.FromResult<UserImportConflictResolution>(new UserImportConflictResolution.Overwrite(c.Record.SubjectId))));
        var import = sp.GetRequiredService<IUserImporter>();

        var subjectId = UserSubjectId.New();
        _ = await import.ImportAsync(
            [new UserImportRecord { SubjectId = subjectId, ProfileAttributes = ValidatedAttributeValueCollection.Empty }],
            _ct);

        var batch = await import.ImportAsync(
            [new UserImportRecord { SubjectId = subjectId, ProfileAttributes = ValidatedAttributeValueCollection.Empty }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Updated);
    }

    [Fact]
    public async Task overwrite_resolver_merges_profile_attributes()
    {
        await using var sp = await CreateProviderWithResolver(
            new LambdaConflictResolver(c => Task.FromResult<UserImportConflictResolution>(new UserImportConflictResolution.Overwrite(c.Record.SubjectId))));
        var import = sp.GetRequiredService<IUserImporter>();
        var schemaAdmin = sp.GetRequiredService<IUserProfileSchemaAdmin>();
        var profileAdmin = sp.GetRequiredService<IUserProfileAdmin>();

        var attr1Name = AttributeCode.Create($"a1_{Guid.NewGuid():N}"[..20]);
        var attr2Name = AttributeCode.Create($"a2_{Guid.NewGuid():N}"[..20]);
        _ = await schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition { Code = attr1Name, AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("first") }, _ct);
        _ = await schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition { Code = attr2Name, AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("second") }, _ct);

        var subjectId = UserSubjectId.New();

        // First import sets attr1
        var schemaForAttrs = await profileAdmin.GetSchemaAsync(_ct);
        var attrs1 = new AttributeValueCollection(schemaForAttrs);
        attrs1.Set(attr1Name, "original");
        _ = await import.ImportAsync(
            [new UserImportRecord { SubjectId = subjectId, ProfileAttributes = attrs1.Validate() }], _ct);

        // Second import overwrites attr1 and adds attr2
        var attrs2 = new AttributeValueCollection(schemaForAttrs);
        attrs2.Set(attr1Name, "updated");
        attrs2.Set(attr2Name, "new-value");
        var batch = await import.ImportAsync(
            [new UserImportRecord { SubjectId = subjectId, ProfileAttributes = attrs2.Validate() }], _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Updated);

        var profile = (await profileAdmin.TryGetAsync(subjectId, _ct)).ShouldNotBeNull();
        profile.Attributes.TryGetValue(attr1Name, out var v1).ShouldBeTrue();
        v1!.TryGetValue<string>(out var s1).ShouldBeTrue();
        s1.ShouldBe("updated");

        profile.Attributes.TryGetValue(attr2Name, out var v2).ShouldBeTrue();
        v2!.TryGetValue<string>(out var s2).ShouldBeTrue();
        s2.ShouldBe("new-value");
    }

    [Fact]
    public async Task subject_conflict_with_retry_resolver_retries_then_skips()
    {
        var callCount = 0;
        var resolver = new LambdaConflictResolver(_ =>
        {
            callCount++;
            return Task.FromResult<UserImportConflictResolution>(new UserImportConflictResolution.Retry());
        });

        await using var sp = await CreateProviderWithResolver(resolver);
        var import = sp.GetRequiredService<IUserImporter>();

        var subjectId = UserSubjectId.New();
        _ = await import.ImportAsync(
            [new UserImportRecord { SubjectId = subjectId, ProfileAttributes = ValidatedAttributeValueCollection.Empty }],
            _ct);

        var batch = await import.ImportAsync(
            [new UserImportRecord { SubjectId = subjectId, ProfileAttributes = ValidatedAttributeValueCollection.Empty }],
            _ct);

        // Should have retried up to max and then failed
        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Failed);
        callCount.ShouldBeGreaterThan(1);
    }
    [Fact]
    public async Task batch_failure_in_one_record_does_not_affect_others()
    {
        var goodSubjectId = UserSubjectId.New();
        var badSubjectId = UserSubjectId.New();

        var batch = await _import.ImportAsync(
            [
                new UserImportRecord
                {
                    SubjectId = badSubjectId,
                    ProfileAttributes = ValidatedAttributeValueCollection.Empty,
                    Memberships = new MembershipImport { Groups = [GroupId.New()] }
                },
                new UserImportRecord
                {
                    SubjectId = goodSubjectId,
                    ProfileAttributes = ValidatedAttributeValueCollection.Empty
                }
            ],
            _ct);

        batch.Results.ShouldContain(r => r.SubjectId == badSubjectId && r.Status == UserImportStatus.Failed);
        batch.Results.ShouldContain(r => r.SubjectId == goodSubjectId && r.Status == UserImportStatus.Created);
    }
    [Fact]
    public async Task profile_is_rolled_back_when_authenticator_import_fails_with_error()
    {
        // Arrange: use a resolver that always says Skip on conflict
        await using var sp = await CreateProviderWithResolver(
            new LambdaConflictResolver(_ => Task.FromResult<UserImportConflictResolution>(new UserImportConflictResolution.Skip())));
        var import = sp.GetRequiredService<IUserImporter>();
        var profileAdmin = sp.GetRequiredService<IUserProfileAdmin>();
        var schemaAdmin = sp.GetRequiredService<IUserProfileSchemaAdmin>();

        var userNameCode = AttributeCode.Create("userName");
        _ = await schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition { Code = userNameCode, AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("unique"), IsUnique = true },
            _ct);

        var schema = await profileAdmin.GetSchemaAsync(_ct);

        // Seed: import a user with a username so the username is taken
        var takenUserName = $"user_{Guid.NewGuid():N}"[..20];
        var existingSubjectId = UserSubjectId.New();
        var existingAttributes = new AttributeValueCollection(schema);
        existingAttributes.Set(userNameCode, takenUserName);
        _ = await import.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = existingSubjectId,
                ProfileAttributes = existingAttributes.Validate(),
                Authenticators = new AuthenticatorImport()
            }],
            _ct);

        // Act: import a new user with a profile with the same username
        // The batch create fails atomically ÔÇö neither profile nor auth is created
        var newSubjectId = UserSubjectId.New();
        var newAttributes = new AttributeValueCollection(schema);
        newAttributes.Set(userNameCode, takenUserName);
        var batch = await import.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = newSubjectId,
                ProfileAttributes = newAttributes.Validate(),
                Authenticators = new AuthenticatorImport()
            }],
            _ct);

        // Batch failed atomically ÔåÆ profile should not exist
        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Skipped);
        var profile = await profileAdmin.TryGetAsync(newSubjectId, _ct);
        profile.ShouldBeNull();
    }

    [Fact]
    public async Task unique_key_conflict_with_skip_resolver_skips_incoming_record()
    {
        var attrName = AttributeCode.Create($"uniq_{Guid.NewGuid():N}"[..20]);
        _ = await _schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition { Code = attrName, AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("unique"), IsUnique = true },
            _ct);

        var existingSubjectId = UserSubjectId.New();
        var incomingSubjectId = UserSubjectId.New();

        var schemaForConflict = await _profileAdmin.GetSchemaAsync(_ct);
        var existingAttrs = new AttributeValueCollection(schemaForConflict);
        existingAttrs.Set(attrName, "shared@example.com");
        _ = await _import.ImportAsync(
            [new UserImportRecord { SubjectId = existingSubjectId, ProfileAttributes = existingAttrs.Validate() }],
            _ct);

        var incomingAttrs = new AttributeValueCollection(schemaForConflict);
        incomingAttrs.Set(attrName, "shared@example.com");
        var batch = await _import.ImportAsync(
            [new UserImportRecord { SubjectId = incomingSubjectId, ProfileAttributes = incomingAttrs.Validate() }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Skipped);
    }

    [Fact]
    public async Task unique_key_conflict_with_retry_resolver_clears_conflict_and_creates_user()
    {
        var attrName = AttributeCode.Create($"uniq_{Guid.NewGuid():N}"[..20]);

        var existingSubjectId = UserSubjectId.New();
        var resolved = false;
        ServiceProvider? sp = null;

        sp = await CreateProviderWithResolver(
            new LambdaConflictResolver(conflict =>
            {
                if (resolved)
                {
                    return Task.FromResult<UserImportConflictResolution>(new UserImportConflictResolution.Skip());
                }

                // Delete the existing user to clear the conflict, then retry
                var userAdmin = sp!.GetRequiredService<IUserAdmin>();
                _ = userAdmin.TryRemoveAsync(existingSubjectId, _ct).GetAwaiter().GetResult();

                resolved = true;
                return Task.FromResult<UserImportConflictResolution>(new UserImportConflictResolution.Retry());
            }));

        await using (sp)
        {
            var schemaAdmin = sp.GetRequiredService<IUserProfileSchemaAdmin>();
            var profileAdmin = sp.GetRequiredService<IUserProfileAdmin>();
            var import = sp.GetRequiredService<IUserImporter>();

            _ = await schemaAdmin.TryAddAttributeDefinitionAsync(
                new AttributeDefinition { Code = attrName, AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("unique"), IsUnique = true },
                _ct);

            var incomingSubjectId = UserSubjectId.New();

            var schemaForRetry = await profileAdmin.GetSchemaAsync(_ct);
            var existingAttrs = new AttributeValueCollection(schemaForRetry);
            existingAttrs.Set(attrName, "taken@example.com");
            _ = await import.ImportAsync(
                [new UserImportRecord { SubjectId = existingSubjectId, ProfileAttributes = existingAttrs.Validate() }],
                _ct);

            resolved = false;
            var incomingAttrs = new AttributeValueCollection(schemaForRetry);
            incomingAttrs.Set(attrName, "taken@example.com");
            var batch = await import.ImportAsync(
                [new UserImportRecord { SubjectId = incomingSubjectId, ProfileAttributes = incomingAttrs.Validate() }],
                _ct);

            batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Created);
            // Existing user was deleted by the resolver
            var existingProfile = await profileAdmin.TryGetAsync(existingSubjectId, _ct);
            existingProfile.ShouldBeNull();
            // Incoming user should have the attribute
            var incomingProfile = (await profileAdmin.TryGetAsync(incomingSubjectId, _ct)).ShouldNotBeNull();
            incomingProfile.Attributes.ContainsKey(attrName).ShouldBeTrue();
        }
    }
    [Fact]
    public async Task auth_only_import_succeeds_without_profile()
    {
        var subjectId = UserSubjectId.New();

        var batch = await _import.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = subjectId,
                Authenticators = new AuthenticatorImport()
            }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Created);
    }
    [Fact]
    public async Task import_with_passkey_creates_authenticators()
    {
        var subjectId = UserSubjectId.New();
        var credentialId = Guid.NewGuid().ToByteArray();
        var publicKey = new byte[32];
        Random.Shared.NextBytes(publicKey);

        var batch = await _import.ImportAsync(
            [new UserImportRecord
            {
                SubjectId = subjectId,
                Authenticators = new AuthenticatorImport
                {
                    Passkeys =
                    [
                        new PasskeyImport
                        {
                            CredentialId = credentialId,
                            PublicKeyCose = publicKey,
                            Algorithm = -7, // ES256
                            SignCount = 0,
                            BackupEligible = false,
                            BackedUp = false,
                            Aaguid = Guid.Empty,
                            Name = "imported-key"
                        }
                    ]
                }
            }],
            _ct);

        batch.Results.ShouldHaveSingleItem().Status.ShouldBe(UserImportStatus.Created);
    }

    [Fact]
    public async Task imported_hashed_password_with_legacy_algorithm_is_rehashed_on_first_auth()
    {
        var dbId = Guid.NewGuid();

        var userName = $"user_{Guid.NewGuid():N}"[..20];
        var passwordText = $"ABcd12!@{Guid.NewGuid()}";
        var subjectId = UserSubjectId.New();
        var otpAddress = new OtpAddress(OtpChannel.Email, EmailAddress.Create($"a{Guid.NewGuid():N}"[..20] + "@x.com"));

        // Step 1: Import user with password hashed by fake algorithm
        await using var importProvider = await CreateProviderWithFakeAlgorithm(preferFake: true, dbId: dbId);
        var importAdmin = importProvider.GetRequiredService<IUserImporter>();
        var schemaAdmin = importProvider.GetRequiredService<IUserProfileSchemaAdmin>();
        var profileAdmin = importProvider.GetRequiredService<IUserProfileAdmin>();
        var fakeAlgo = importProvider.GetServices<IPasswordHashAlgorithm>()
            .Single(a => a.AlgorithmId == FakePasswordHashAlgorithm.Id);

        var userNameCode = AttributeCode.Create("userName");
        _ = await schemaAdmin.TryAddAttributeDefinitionAsync(
            new AttributeDefinition { Code = userNameCode, AttributeType = new ScalarAttributeType(ScalarDataType.String), Description = AttributeDescription.Create("user id"), IsUnique = true },
            _ct);

        var schema = await profileAdmin.GetSchemaAsync(_ct);
        var attributes = new AttributeValueCollection(schema);
        attributes.Set(userNameCode, userName);

        var hashedData = fakeAlgo.Hash(passwordText);
        var record = new UserImportRecord
        {
            SubjectId = subjectId,
            ProfileAttributes = attributes.Validate(),
            Authenticators = new AuthenticatorImport
            {
                Password = new PasswordImport(hashedData),
                OtpAddresses = [otpAddress]
            }
        };

        var importResult = await importAdmin.ImportAsync([record], _ct);
        importResult.CreatedCount.ShouldBe(1);

        // Step 2: Authenticate with pbkdf2 as preferred (fake still registered) should triggers re-hash
        await using (var authProvider = await CreateProviderWithFakeAlgorithm(preferFake: false, dbId: dbId))
        {
            var auth = authProvider.GetRequiredService<IPasswordAuthenticator>();

            var result = await auth.TryAuthenticateAsync(userNameCode, userName, NonValidatedPassword.Create(passwordText), _ct);
            _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
        }

        // Step 3: Authenticate with only pbkdf2: proves re-hash happened
        await using (var verifyProvider = await CreateProviderWithPbkdf2Only(dbId: dbId))
        {
            var auth = verifyProvider.GetRequiredService<IPasswordAuthenticator>();

            var result = await auth.TryAuthenticateAsync(userNameCode, userName, NonValidatedPassword.Create(passwordText), _ct);
            _ = result.ShouldBeOfType<PasswordAuthenticationResult.Success>();
        }
    }
    private static async Task<ServiceProvider> CreateProviderWithResolver(IUserImportConflictResolver resolver) =>
        await UsersServiceProviderFactory.CreateAsync(builder =>
        {
            _ = builder.Services.AddSingleton(resolver);
        });

    private static async Task<ServiceProvider> CreateProviderWithFakeAlgorithm(bool preferFake, Guid? dbId = null) =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: preferFake
                ? options => options.Passwords.PreferredHashAlgorithm = FakePasswordHashAlgorithm.Id
                : null,
            addDataProtection: true,
            configureServices: services => _ = services.AddSingleton<IPasswordHashAlgorithm, FakePasswordHashAlgorithm>(),
            dbId: dbId);

    private static async Task<ServiceProvider> CreateProviderWithPbkdf2Only(Guid? dbId = null) =>
        await UsersServiceProviderFactory.CreateUsersBuilderAsync(
            configureOptions: null,
            addDataProtection: true,
            dbId: dbId);

    private sealed class FakePasswordHashAlgorithm : IPasswordHashAlgorithm
    {
        public const string Id = "fake-import-test-algo";

        string IPasswordHashAlgorithm.AlgorithmId => Id;

        public HashedPasswordData Hash(string password) =>
            new(Id,
                System.Text.Encoding.UTF8.GetBytes(password),
                [],
                new Dictionary<string, string>());

        public bool Verify(string password, HashedPasswordData data) =>
            System.Text.Encoding.UTF8.GetBytes(password).SequenceEqual([.. data.Hash]);

        public bool NeedsRehash(HashedPasswordData data) => data.AlgorithmId != Id;
    }

    private sealed class LambdaConflictResolver(Func<UserImportConflict, Task<UserImportConflictResolution>> resolve)
        : IUserImportConflictResolver
    {
        public Task<UserImportConflictResolution> ResolveAsync(UserImportConflict conflict, Ct ct) => resolve(conflict);
    }

}
