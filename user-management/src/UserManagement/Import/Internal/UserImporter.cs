// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage;
using Duende.Storage.EntityAttributeValue;
using Duende.Storage.EntityAttributeValue.Internal;
using Duende.Storage.Internal.Operations;
using Duende.UserManagement.Authentication.Internal;
using Duende.UserManagement.Authentication.Internal.Storage;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Duende.UserManagement.Internal;
using Duende.UserManagement.Internal.Licensing;
using Duende.UserManagement.Internal.Storage;
using Duende.UserManagement.Membership.Internal;
using Duende.UserManagement.Membership.Internal.Storage;
using Duende.UserManagement.Profiles;
using Duende.UserManagement.Profiles.Internal;
using Duende.UserManagement.Profiles.Internal.Storage;
using Microsoft.Extensions.Logging;

namespace Duende.UserManagement.Import.Internal;

#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserImporter(
    IUserImportConflictResolver conflictResolver,
    TimeProvider timeProvider,
    IStoreFactory storeFactory,
    ILogger<UserImporter> logger,
    UserManagementLicenseValidator licenseValidator,
    UserProfileRepository profileRepo,
    UserAuthenticatorsRepository authenticatorsRepo,
    AttributeSchemaRepository schemaRepo,
    GroupRepository groupRepo,
    RoleRepository roleRepo,
    MembershipRepository membershipRepo) : IUserImporter
{
    private const int MaxAttempts = 3;

    public async Task<UserImportBatchResult> ImportAsync(IReadOnlyList<UserImportRecord> records, Ct ct)
    {
        licenseValidator.ValidateRegistration();
        logger.BatchImportStarted(LogLevel.Debug, records.Count);

        // Load schema once for the entire batch (only needed if any record has profile attributes)
        AttributeSchema? schema = null;
        if (records.Any(r => r.ProfileAttributes is not null))
        {
            schema = (await schemaRepo.TryReadAsync(UserProfileSchemaId.Value, ct))?.AttributeSchema
                ?? AttributeSchema.Empty;

            // Validate schema freshness for all records upfront — fail the entire batch on mismatch
            var currentSchema = schema;
            foreach (var record in records)
            {
                if (record.ProfileAttributes is not null &&
                    !SchemaFreshnessCheck.IsValid(record.ProfileAttributes, currentSchema, logger))
                {
                    return new UserImportBatchResult
                    {
                        Results = records.Select(r => Fail(r.SubjectId,
                            "Schema version mismatch: the batch was validated against a stale schema.")).ToList()
                    };
                }
            }
        }

        var results = new List<UserImportResult>(records.Count);

        foreach (var record in records)
        {
            var result = await ImportRecordAsync(record, schema, ct);
            results.Add(result);
        }

        var batchResult = new UserImportBatchResult { Results = results };
        var successCount = results.Count(r => r.Status == UserImportStatus.Created);
        var updatedCount = results.Count(r => r.Status == UserImportStatus.Updated);
        var skippedCount = results.Count(r => r.Status == UserImportStatus.Skipped);
        var failedCount = results.Count(r => r.Status == UserImportStatus.Failed);
        logger.BatchImportCompleted(LogLevel.Debug, successCount, updatedCount, skippedCount, failedCount);
        return batchResult;
    }

    private async Task<UserImportResult> ImportRecordAsync(
        UserImportRecord record,
        AttributeSchema? schema,
        Ct ct)
    {
        using var scope = logger.BeginSubjectScope(record.SubjectId);
        // 1. Validate profile attributes against schema (if provided)
        if (record.ProfileAttributes is not null)
        {
            var currentSchema = schema ?? AttributeSchema.Empty;
            if (!SchemaFreshnessCheck.IsValid(record.ProfileAttributes, currentSchema, logger))
            {
                return Fail(record.SubjectId, "Schema version mismatch: the profile attributes were validated against a stale schema.");
            }
        }


        // 2. Validate membership references exist before attempting batch
        if (record.Memberships is not null)
        {
            var membershipError = await ValidateMembershipReferencesAsync(record.Memberships, ct);
            if (membershipError is not null)
            {
                logger.RecordValidationFailed(LogLevel.Debug, membershipError);
                return Fail(record.SubjectId, membershipError);
            }
        }

        // 3. Attempt atomic batch create with retry loop
        return await TryBatchCreateWithConflictResolutionAsync(record, schema, ct);
    }

    private async Task<UserImportResult> TryBatchCreateWithConflictResolutionAsync(UserImportRecord record, AttributeSchema? schema, Ct ct)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var (operations, userDsoIndex, profileIndex, authIndex, _) = await BuildBatchOperationsAsync(record, ct);

            if (operations.Count == 0)
            {
                return new UserImportResult { SubjectId = record.SubjectId, Status = UserImportStatus.Skipped };
            }

            var store = storeFactory.GetStore();
            var result = await store.ExecuteBatchAsync(operations, [], ct);

            if (result.Success)
            {
                return new UserImportResult { SubjectId = record.SubjectId, Status = UserImportStatus.Created };
            }

            // Determine conflict reason from batch results
            var conflict = BuildConflictFromBatchResult(record, result, userDsoIndex, profileIndex, authIndex);
            logger.ConflictDetected(LogLevel.Debug, conflict.Step, conflict.Reason);
            var resolution = await conflictResolver.ResolveAsync(conflict, ct);

            switch (resolution)
            {
                case UserImportConflictResolution.Skip:
                    logger.ConflictResolutionApplied(LogLevel.Debug, "Skip");
                    return Skip(record.SubjectId);

                case UserImportConflictResolution.Overwrite overwrite:
                    logger.ConflictResolutionApplied(LogLevel.Debug, "Overwrite");
                    return await OverwriteExistingUserAsync(record, overwrite.TargetSubjectId, schema, ct);

                case UserImportConflictResolution.Retry:
                    logger.RetryTriggered(LogLevel.Debug, attempt + 1, MaxAttempts);
                    continue;
            }
        }

        return Fail(record.SubjectId, "Max retries exceeded for import.");
    }

    private async Task<(List<IStoreOperation> Operations, int UserDsoIndex, int ProfileIndex, int AuthIndex, int MembershipLinkStartIndex)> BuildBatchOperationsAsync(
        UserImportRecord record, Ct ct)
    {
        List<IStoreOperation> operations = [];
        var profileIndex = -1;
        var authIndex = -1;
        var membershipLinkStartIndex = -1;
        List<UserDso.AspectRef> aspectRefs = [];

        // Pre-generate UserDso UUID so membership links can reference it
        var userDsoId = UuidV7.New();

        // Build aspect operations and collect refs
        CreateOperation? profileOp = null;
        CreateOperation? authOp = null;

        if (record.ProfileAttributes is not null)
        {
            var profile = new Profiles.Internal.UserProfile(record.SubjectId, record.ProfileAttributes);

            if (record.UserName is not null)
            {
                profile.SetUserName(record.UserName.Value);
            }

            var (aspectOp, aspectRef) = await profileRepo.CreateAspectBatchOperationAsync(profile, ct);
            profileOp = aspectOp;
            aspectRefs.Add(aspectRef);
        }

        if (record.Authenticators is not null)
        {
            var authenticators = BuildAuthenticators(record.SubjectId, record.Authenticators, record.UserName);
            authOp = authenticatorsRepo.CreateAspectBatchOperation(authenticators);
            aspectRefs.Add(UserAuthenticatorsRepository.GetAspectRef(authenticators));
        }

        // UserDso is always first
        var userDsoIndex = operations.Count;
        operations.Add(UserRepository.CreateBatchOperation(userDsoId, record.SubjectId, record.UserName, aspectRefs));

        if (profileOp is not null)
        {
            profileIndex = operations.Count;
            operations.Add(profileOp);
        }

        if (authOp is not null)
        {
            authIndex = operations.Count;
            operations.Add(authOp);
        }

        if (record.Memberships is not null)
        {
            membershipLinkStartIndex = operations.Count;

            if (record.Memberships.Groups is not null)
            {
                foreach (var groupId in record.Memberships.Groups.Distinct())
                {
                    var (resolvedGroup, _) = await groupRepo.TryReadAsync(groupId, ct)
                        ?? throw new InvalidOperationException($"Group '{groupId}' not found during import.");
                    operations.Add(LinkOperation.For(MembershipLinkDefinitions.MembershipGroup, userDsoId, resolvedGroup.StoreId));
                }
            }

            if (record.Memberships.DirectRoles is not null)
            {
                foreach (var roleId in record.Memberships.DirectRoles.Distinct())
                {
                    var (resolvedRole, _) = await roleRepo.TryReadAsync(roleId, ct)
                        ?? throw new InvalidOperationException($"Role '{roleId}' not found during import.");
                    operations.Add(LinkOperation.For(MembershipLinkDefinitions.MembershipRole, userDsoId, resolvedRole.StoreId));
                }
            }
        }

        return (operations, userDsoIndex, profileIndex, authIndex, membershipLinkStartIndex);
    }

    private static UserImportConflict BuildConflictFromBatchResult(
        UserImportRecord record,
        BatchResult result,
        int userDsoIndex,
        int profileIndex,
        int authIndex)
    {
        // Check UserDso operation first (it's at the front of the batch)
        if (userDsoIndex >= 0 && userDsoIndex < result.Results.Count)
        {
            var userDsoOutcome = result.Results[userDsoIndex].Outcome;
            if (userDsoOutcome is OperationOutcome.AlreadyExists or OperationOutcome.KeyConflict)
            {
                // Subject already exists — determine the most specific reason based on what was being imported
                if (profileIndex >= 0)
                {
                    return BuildConflict(record, UserImportStep.UserRecord, UserImportConflictReason.ProfileAlreadyExists);
                }

                if (authIndex >= 0)
                {
                    return BuildConflict(record, UserImportStep.UserRecord, UserImportConflictReason.AuthenticatorAlreadyExists);
                }

                return BuildConflict(record, UserImportStep.UserRecord, UserImportConflictReason.MembershipAlreadyExists);
            }
        }

        // Check aspect operations (higher indices, may be out of bounds if batch aborted early)

        // Check profile operation
        if (profileIndex >= 0 && profileIndex < result.Results.Count)
        {
            var profileOutcome = result.Results[profileIndex].Outcome;
            if (profileOutcome is OperationOutcome.AlreadyExists)
            {
                return BuildConflict(record, UserImportStep.Profile, UserImportConflictReason.ProfileAlreadyExists);
            }

            if (profileOutcome is OperationOutcome.KeyConflict)
            {
                return BuildConflict(record, UserImportStep.Profile, UserImportConflictReason.ProfileUniqueKeyConflict);
            }
        }

        // Then check authenticator operation
        if (authIndex >= 0 && authIndex < result.Results.Count)
        {
            var authOutcome = result.Results[authIndex].Outcome;
            if (authOutcome is OperationOutcome.AlreadyExists)
            {
                return BuildConflict(record, UserImportStep.Authenticator, UserImportConflictReason.AuthenticatorAlreadyExists);
            }

            if (authOutcome is OperationOutcome.KeyConflict)
            {
                return BuildConflict(record, UserImportStep.Authenticator, UserImportConflictReason.AuthenticatorKeyConflict);
            }
        }

        // Determine which operation actually failed for better diagnostics
        var failedStep = UserImportStep.Profile;
        if (profileIndex >= 0 && profileIndex < result.Results.Count && result.Results[profileIndex].Outcome is OperationOutcome.UnexpectedVersion)
        {
            failedStep = UserImportStep.Profile;
        }
        else if (authIndex >= 0 && authIndex < result.Results.Count && result.Results[authIndex].Outcome is not OperationOutcome.Success)
        {
            failedStep = UserImportStep.Authenticator;
        }
        else if (userDsoIndex >= 0 && userDsoIndex < result.Results.Count && result.Results[userDsoIndex].Outcome is not OperationOutcome.Success)
        {
            failedStep = UserImportStep.UserRecord;
        }

        return BuildConflict(record, failedStep, UserImportConflictReason.ConcurrencyConflict);
    }

    private async Task<UserImportResult> OverwriteExistingUserAsync(
        UserImportRecord record,
        UserSubjectId targetSubjectId,
        AttributeSchema? schema,
        Ct ct)
    {
        var userName = record.UserName;

        if (record.ProfileAttributes is not null || userName is not null)
        {
            var mergeError = await MergeProfileAsync(targetSubjectId, record.ProfileAttributes, userName, schema, ct);
            if (mergeError is not null)
            {
                return Fail(record.SubjectId, mergeError);
            }
        }

        if (record.Authenticators is not null)
        {
            var mergeError = await MergeAuthenticatorsAsync(targetSubjectId, record.Authenticators, record.UserName, ct);
            if (mergeError is not null)
            {
                return Fail(record.SubjectId, mergeError);
            }
        }

        if (record.Memberships is not null)
        {
            var membershipError = await MergeMembershipsAsync(targetSubjectId, record.Memberships, ct);
            if (membershipError is not null)
            {
                return Fail(record.SubjectId, membershipError);
            }
        }

        return new UserImportResult { SubjectId = record.SubjectId, Status = UserImportStatus.Updated };
    }

    private async Task<string?> MergeProfileAsync(UserSubjectId subjectId, ValidatedAttributeValueCollection? attributes, UserName? userName, AttributeSchema? schema, Ct ct)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var existing = await profileRepo.TryReadAsync(subjectId, ct);
            if (existing is not ({ } profile, var version))
            {
                return "Profile not found for merge.";
            }

            if (attributes is not null)
            {
                var effectiveSchema = schema ?? AttributeSchema.Empty;
                var merged = new AttributeValueCollection(effectiveSchema);
                foreach (var attr in profile.Attributes.Values)
                {
                    if (effectiveSchema.AttributeDefinitions.ContainsKey(attr.Code))
                    {
                        merged.Set(attr);
                    }
                }

                foreach (var attr in attributes)
                {
                    merged.Set(attr);
                }

                profile.ReplaceAttributes(merged.Validate());
            }

            if (userName is not null)
            {
                profile.SetUserName(userName.Value);
            }

            var updateResult = await profileRepo.UpdateAsync(profile, version, ct);
            if (updateResult is UpdateResult.Success)
            {
                return null;
            }

            if (updateResult is UpdateResult.UnexpectedVersion)
            {
                continue;
            }

            return "Failed to merge profile attributes.";
        }

        return "Failed to merge profile attributes after retries.";
    }

    private async Task<string?> MergeAuthenticatorsAsync(UserSubjectId subjectId, AuthenticatorImport import, UserName? userName, Ct ct)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var existing = await authenticatorsRepo.TryReadAsync(subjectId, ct);
            if (existing is not ({ } authenticators, var version))
            {
                var created = BuildAuthenticators(subjectId, import, userName);
                var createResult = await authenticatorsRepo.CreateAsync(created, ct);
                if (createResult is not CreateResult.Success)
                {
                    return "Failed to create authenticators during merge.";
                }

                return null;
            }

            if (userName is not null)
            {
                authenticators.SetUserName(userName.Value);
            }

            if (import.Password is not null)
            {
                var hashedPassword = BuildHashedPassword(import.Password);
                authenticators.LoadHashedPassword(hashedPassword);
            }

            if (import.OtpAddresses is not null)
            {
                authenticators.Add(import.OtpAddresses);
            }

            if (import.ExternalAuthenticators is not null)
            {
                authenticators.Add(import.ExternalAuthenticators);
            }

            if (import.Passkeys is not null)
            {
                foreach (var passkey in import.Passkeys)
                {
                    var credential = PasskeyCredential.Create(
                        timeProvider,
                        PasskeyCredentialId.From([.. passkey.CredentialId]),
                        [.. passkey.PublicKeyCose],
                        passkey.Algorithm,
                        passkey.SignCount,
                        passkey.BackupEligible,
                        passkey.BackedUp,
                        passkey.Aaguid,
                        passkey.Name);

                    _ = authenticators.TryAdd(credential);
                }
            }

            if (import.TotpAuthenticators is not null)
            {
                foreach (var totp in import.TotpAuthenticators)
                {
                    authenticators.LoadTotpAuthenticator(totp.Name, totp.Key);
                }
            }

            if (import.RecoveryCodes is not null)
            {
                authenticators.LoadRecoveryCodes(import.RecoveryCodes);
            }

            var updateResult = await authenticatorsRepo.UpdateAsync(authenticators, version, ct);
            if (updateResult is UpdateResult.Success)
            {
                return null;
            }

            if (updateResult is UpdateResult.UnexpectedVersion)
            {
                continue;
            }

            return "Failed to update authenticators during merge.";
        }

        return "Failed to update authenticators after retries.";
    }

    private UserAuthenticators BuildAuthenticators(UserSubjectId subjectId, AuthenticatorImport import, UserName? userName)
    {
        var otpAddresses = import.OtpAddresses ?? [];
        var externalAuthenticators = import.ExternalAuthenticators ?? [];

        var authenticators = new UserAuthenticators(subjectId, otpAddresses, externalAuthenticators);

        if (userName is not null)
        {
            authenticators.SetUserName(userName.Value);
        }

        if (import.Password is not null)
        {
            var hashedPassword = BuildHashedPassword(import.Password);
            authenticators.LoadHashedPassword(hashedPassword);
        }

        if (import.Passkeys is not null)
        {
            foreach (var passkey in import.Passkeys)
            {
                var credential = PasskeyCredential.Create(
                    timeProvider,
                    PasskeyCredentialId.From([.. passkey.CredentialId]),
                    [.. passkey.PublicKeyCose],
                    passkey.Algorithm,
                    passkey.SignCount,
                    passkey.BackupEligible,
                    passkey.BackedUp,
                    passkey.Aaguid,
                    passkey.Name);

                _ = authenticators.TryAdd(credential);
            }
        }

        if (import.TotpAuthenticators is not null)
        {
            foreach (var totp in import.TotpAuthenticators)
            {
                authenticators.LoadTotpAuthenticator(totp.Name, totp.Key);
            }
        }

        if (import.RecoveryCodes is not null)
        {
            authenticators.LoadRecoveryCodes(import.RecoveryCodes);
        }

        return authenticators;
    }

    private static HashedPassword BuildHashedPassword(PasswordImport passwordImport) =>
        HashedPassword.Load(passwordImport.Data);

    private async Task<string?> ValidateMembershipReferencesAsync(MembershipImport import, Ct ct)
    {
        if (import.Groups is not null)
        {
            foreach (var groupId in import.Groups)
            {
                if (!(await groupRepo.TryReadAsync(groupId, ct)).HasValue)
                {
                    return $"Group '{groupId}' does not exist.";
                }
            }
        }

        if (import.DirectRoles is not null)
        {
            foreach (var roleId in import.DirectRoles)
            {
                if (!(await roleRepo.TryReadAsync(roleId, ct)).HasValue)
                {
                    return $"Role '{roleId}' does not exist.";
                }
            }
        }

        return null;
    }

    private async Task<string?> MergeMembershipsAsync(UserSubjectId subjectId, MembershipImport import, Ct ct)
    {
        var userUuid = await membershipRepo.GetOrCreateUserUuidAsync(subjectId, ct);
        var store = storeFactory.GetStore();

        if (import.Groups is not null)
        {
            foreach (var groupId in import.Groups)
            {
                var (resolvedGroup, _) = await groupRepo.TryReadAsync(groupId, ct)
                    ?? throw new InvalidOperationException($"Group '{groupId}' not found during import.");
                _ = await store.LinkAsync(MembershipLinkDefinitions.MembershipGroup, userUuid, resolvedGroup.StoreId, [], ct);
            }
        }

        if (import.DirectRoles is not null)
        {
            foreach (var roleId in import.DirectRoles)
            {
                var (resolvedRole, _) = await roleRepo.TryReadAsync(roleId, ct)
                    ?? throw new InvalidOperationException($"Role '{roleId}' not found during import.");
                _ = await store.LinkAsync(MembershipLinkDefinitions.MembershipRole, userUuid, resolvedRole.StoreId, [], ct);
            }
        }

        return null;
    }

    private static UserImportConflict BuildConflict(UserImportRecord record, UserImportStep step, UserImportConflictReason reason) =>
        new()
        {
            Record = record,
            Step = step,
            Reason = reason,
            Exception = new InvalidOperationException($"Import conflict: {reason} at {step} for subject {record.SubjectId}.")
        };

    private static UserImportResult Skip(UserSubjectId subjectId) =>
        new() { SubjectId = subjectId, Status = UserImportStatus.Skipped };

    private static UserImportResult Fail(UserSubjectId subjectId, string error) =>
        new() { SubjectId = subjectId, Status = UserImportStatus.Failed, Error = error };
}
