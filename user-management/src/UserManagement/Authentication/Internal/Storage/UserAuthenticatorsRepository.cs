// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Storage.Internal;
using Duende.Storage.Internal.Operations;
using Duende.Storage.Internal.Querying.Expressions;
using Duende.Storage.Internal.Querying.Sorting;
using Duende.Storage.Pagination;
using Duende.Storage.Querying;
using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.External.Internal.Storage;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Otp.Internal.Storage;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Duende.UserManagement.Authentication.Passkeys.Internal.Storage;
using Duende.UserManagement.Authentication.Totp;
using Duende.UserManagement.Authentication.Totp.Internal;
using Duende.UserManagement.Authentication.Totp.Internal.Storage;
using Duende.UserManagement.Internal.Storage;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Duende.UserManagement.Authentication.Internal.Storage;
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class UserAuthenticatorsRepository(
    IStoreFactory storeFactory,
    IOptions<UserAuthenticatorsRepository.Options> options,
    IDataProtectionProvider dataProtectionProvider,
    UserRepository userRepository)
{
    internal enum Keys
    {
        ExternalAuthenticator = 1,
        OtpAddress = 2,
        SubjectId = 3,
        UserName = 4,
        PasskeyCredentialId = 5
    }

    private readonly IDataProtector _dataProtector = dataProtectionProvider.CreateProtector(nameof(UserAuthenticators));
    private readonly Options _options = options.Value;

    internal async Task<CreateResult> CreateAsync(UserAuthenticators authenticators, Ct ct)
    {
        var store = storeFactory.GetStore();
        var operations = await BuildCreateOperationsAsync(authenticators, ct);
        var result = await store.ExecuteBatchAsync(operations, [], ct);
        if (result.Success)
        {
            return CreateResult.Success;
        }

        var firstFailure = result.Results.First(r => r.Outcome is not OperationOutcome.Success).Outcome;
        return firstFailure switch
        {
            OperationOutcome.KeyConflict => CreateResult.KeyConflict,
            OperationOutcome.AlreadyExists => CreateResult.AlreadyExists,
            _ => CreateResult.ConcurrencyConflict
        };
    }

    internal async Task<IReadOnlyList<IStoreOperation>> CreateBatchOperationAsync(UserAuthenticators authenticators, Ct ct) =>
        await BuildCreateOperationsAsync(authenticators, ct);

    internal CreateOperation CreateAspectBatchOperation(UserAuthenticators authenticators) =>
        CreateOperation.For(
            authenticators.Id.Uuid,
            ToDso(authenticators),
            [
                DataStorageKey.Create(UserSubjectIdDskV1.Create(authenticators.SubjectId)),
                .. GetJsonKeys(authenticators)
            ],
            [],
            Expiration.NoExpiration);

    internal static UserDso.AspectRef GetAspectRef(UserAuthenticators authenticators) =>
        new(authenticators.Id.Uuid.Value, 1, UserAuthenticatorsDso.EntityType.Id);

    internal static UserDso.AspectRef GetAspectRef(UserAuthenticators authenticators, int version) =>
        new(authenticators.Id.Uuid.Value, version, UserAuthenticatorsDso.EntityType.Id);

    internal async Task<(UserAuthenticators UserAuthenticators, int Version)?> TryReadAsync(UserSubjectId subjectId, Ct ct)
    {
        var store = storeFactory.GetStore();
        var result = await store.TryReadAsync(UserAuthenticatorsDso.EntityType, DataStorageKey.Create(UserSubjectIdDskV1.Create(subjectId)), ct);
        return result.Found ? (ToEntity(result.Dso), result.Version.Value) : null;
    }

    internal async Task<(UserAuthenticators UserAuthenticators, int Version)?> TryReadAsync(OtpAddress otpAddress, Ct ct)
    {
        var store = storeFactory.GetStore();
        var result = await store.TryReadAsync(UserAuthenticatorsDso.EntityType, DataStorageKey.Create(OtpAddressDskV1.Create(otpAddress)), ct);
        return result.Found ? (ToEntity(result.Dso), result.Version.Value) : null;
    }

    internal async Task<(UserAuthenticators UserAuthenticators, int Version)?> TryReadAsync(
        ExternalAuthenticator externalAuthenticator,
        Ct ct)
    {
        var store = storeFactory.GetStore();
        var result = await store.TryReadAsync(UserAuthenticatorsDso.EntityType, DataStorageKey.Create(ExternalAuthenticatorDskV1.Create(externalAuthenticator)), ct);
        return result.Found ? (ToEntity(result.Dso), result.Version.Value) : null;
    }

    internal async Task<(UserAuthenticators UserAuthenticators, int Version)?> TryReadAsync(UserName userName, Ct ct)
    {
        var store = storeFactory.GetStore();
        var result = await store.TryReadAsync(UserAuthenticatorsDso.EntityType, DataStorageKey.Create(UserNameDskV1.Create(userName)), ct);
        return result.Found ? (ToEntity(result.Dso), result.Version.Value) : null;
    }

    internal async Task<(UserAuthenticators UserAuthenticators, int Version)?> TryReadAsync(
        PasskeyCredentialId credentialId, Ct ct)
    {
        var store = storeFactory.GetStore();
        var result = await store.TryReadAsync(UserAuthenticatorsDso.EntityType, DataStorageKey.Create(PasskeyCredentialIdDskV1.Create(credentialId)), ct);
        return result.Found ? (ToEntity(result.Dso), result.Version.Value) : null;
    }

    private UserAuthenticators ToEntity(IDataStorageObject value) =>
        value switch
        {
            UserAuthenticatorsDso.V1 v1 => ToEntity(v1),
            _ => throw new InvalidOperationException($"Unexpected type: {value.GetType().Name}")
        };

    internal async Task<UpdateResult> UpdateAsync(UserAuthenticators authenticators, int expectedVersion, Ct ct)
    {
        var store = storeFactory.GetStore();
        var operations = await BuildUpdateOperationsAsync(authenticators, expectedVersion, ct);
        var result = await store.ExecuteBatchAsync(operations, [], ct);
        if (result.Success)
        {
            return UpdateResult.Success;
        }

        // Find the first failed operation across the batch
        var firstFailure = result.Results.First(r => r.Outcome is not OperationOutcome.Success).Outcome;
        return firstFailure switch
        {
            OperationOutcome.KeyConflict => UpdateResult.KeyConflict,
            OperationOutcome.DoesNotExist => UpdateResult.DoesNotExist,
            _ => UpdateResult.UnexpectedVersion
        };
    }

    internal async Task<IReadOnlyList<IStoreOperation>> UpdateBatchOperationAsync(UserAuthenticators authenticators, int expectedVersion, Ct ct) =>
        await BuildUpdateOperationsAsync(authenticators, expectedVersion, ct);

    internal UpdateOperation UpdateAspectOnlyBatchOperation(UserAuthenticators authenticators, int expectedVersion) =>
        UpdateOperation.For(
            authenticators.Id.Uuid,
            ToDso(authenticators),
            expectedVersion,
            [
                DataStorageKey.Create(UserSubjectIdDskV1.Create(authenticators.SubjectId)),
                .. GetJsonKeys(authenticators)
            ],
            [],
            Expiration.NoExpiration);

    internal static DeleteOperation DeleteBatchOperation(UserSubjectId subjectId) =>
        DeleteOperation.ByKey(UserAuthenticatorsDso.EntityType, DataStorageKey.Create(UserSubjectIdDskV1.Create(subjectId)));

    internal async Task<QueryResult<UserAuthenticators>> QueryAsync(DataRange? range, Ct ct)
    {
        var queryStore = storeFactory.GetStore();
        var dataRange = range ?? DataRange.FromPage(1, DataRangeSize.Default);
        if (dataRange.TokenValue is not null)
        {
            throw new NotSupportedException("User authenticator queries do not support continuation-token pagination.");
        }

        var result = await queryStore.QueryAsync<UserAuthenticatorsDso.V1>(
            UserAuthenticatorsDso.EntityType,
            AllExpression.Instance,
            SortParameter.Empty,
            dataRange,
            ct);

        return result.ConvertTo(envelope => ToEntity(envelope.Value));
    }

    private async Task<List<IStoreOperation>> BuildCreateOperationsAsync(UserAuthenticators authenticators, Ct ct)
    {
        var aspectRef = new UserDso.AspectRef(authenticators.Id.Uuid.Value, 1, UserAuthenticatorsDso.EntityType.Id);
        var existingUser = await userRepository.TryReadAsync(authenticators.SubjectId, ct);

        // Inherit username from the root UserDso if the authenticator doesn't have one
        if (authenticators.UserName is null && existingUser is var (existingUserDso, _) && existingUserDso.UserName is not null)
        {
            authenticators.SetUserName(UserName.Load(existingUserDso.UserName));
        }

        var aspectOp = CreateOperation.For(
            authenticators.Id.Uuid,
            ToDso(authenticators),
            [
                DataStorageKey.Create(UserSubjectIdDskV1.Create(authenticators.SubjectId)),
                .. GetJsonKeys(authenticators)
            ],
            [],
            Expiration.NoExpiration);

        IStoreOperation userOp = existingUser is var (user, userVersion)
            ? UserRepository.UpdateBatchOperation(
                UserRepository.AddOrUpdateAspectRef(user with { UserName = authenticators.UserName?.Value ?? user.UserName }, aspectRef),
                userVersion)
            : UserRepository.CreateBatchOperation(authenticators.SubjectId, authenticators.UserName, [aspectRef]);

        return [userOp, aspectOp];
    }

    private async Task<List<IStoreOperation>> BuildUpdateOperationsAsync(UserAuthenticators authenticators, int expectedVersion, Ct ct)
    {
        var aspectOp = UpdateOperation.For(
            authenticators.Id.Uuid,
            ToDso(authenticators),
            expectedVersion,
            [
                DataStorageKey.Create(UserSubjectIdDskV1.Create(authenticators.SubjectId)),
                .. GetJsonKeys(authenticators)
            ],
            [],
            Expiration.NoExpiration);

        var aspectRef = new UserDso.AspectRef(authenticators.Id.Uuid.Value, expectedVersion + 1, UserAuthenticatorsDso.EntityType.Id);
        var existingUser = await userRepository.TryReadAsync(authenticators.SubjectId, ct);

        IStoreOperation userOp = existingUser is var (user, userVersion)
            ? UserRepository.UpdateBatchOperation(
                UserRepository.AddOrUpdateAspectRef(user with { UserName = authenticators.UserName?.Value ?? user.UserName }, aspectRef),
                userVersion)
            : UserRepository.CreateBatchOperation(authenticators.SubjectId, authenticators.UserName, [aspectRef]);

        return [userOp, aspectOp];
    }

    private UserAuthenticatorsDso.V1 ToDso(UserAuthenticators entity) => new(
        entity.Id.Uuid.Value,
        entity.SubjectId.Value,
        [.. entity.OtpAddresses.Select(a => new OtpAddressDso.V1(a.Channel.Value, a.SubjectId.ToDso()))],
        [
            .. entity.ExternalAuthenticators.Select(a =>
                new ExternalAuthenticatorDso.V1(a.Name.Value, a.SubjectId.ToDso()))
        ],
        [.. entity.TotpAuthenticators.Values.Select(ToDso)],
        [.. entity.RecoveryCodes.Select(a => a.ToDso())],
        entity.UserName?.Value,
        entity.HashedPassword?.ToDso(),
        [.. entity.PasskeyCredentials.Values.Select(c => new PasskeyCredentialDso.V1(
            c.CredentialId.ToBytes(),
            [.. c.PublicKeyCose],
            c.Algorithm,
            c.SignCount,
            c.BackupEligible,
            c.BackedUp,
            c.Aaguid,
            c.CreatedAt,
            c.Name))],
        [.. entity.FailureStates.Select(ToDso)],
        entity.PasswordHistory.Count > 0
            ? [.. entity.PasswordHistory.Select(h => h.ToDso())]
            : null,
        entity.PasswordSetAtUtc);

    private UserAuthenticators ToEntity(UserAuthenticatorsDso.V1 dso) => UserAuthenticators.Load(
        UserAuthenticatorsId.Load(dso.Id),
        UserSubjectId.Load(dso.SubjectId),
        dso.OtpAddresses.Select(a => OtpAddress.Load(OtpChannel.Load(a.Channel), a.SubjectId.ToValueObject())),
        dso.ExternalAuthenticators.Select(a =>
            ExternalAuthenticator.Load(ExternalAuthenticatorName.Load(a.Name), a.SubjectId.ToValueObject())),
        [.. dso.TotpAuthenticators.Select(ToValueObject)],
        dso.RecoveryCodes.Select(a => a.ToValueObject()),
        dso.UserName is not null ? UserName.Load(dso.UserName) : (UserName?)null,
        dso.HashedPassword?.ToValueObject(),
        dso.PasskeyCredentials.Select(c => new PasskeyCredential(
            PasskeyCredentialId.Load(c.CredentialId),
            c.PublicKeyCose,
            c.Algorithm,
            c.SignCount,
            c.BackupEligible,
            c.BackedUp,
            c.Aaguid,
            c.CreatedAt,
            c.Name)),
        (dso.FailureStates ?? []).Select(ToFailureState),
        (dso.PasswordHistory ?? []).Select(h => h.ToValueObject()),
        dso.PasswordSetAtUtc);

    private static AuthenticatorFailureStateDso.V1 ToDso(
        KeyValuePair<AuthenticatorKey, AuthenticatorFailureState> failureState) =>
        failureState.Key switch
        {
            AuthenticatorKey.Password => new AuthenticatorFailureStateDso.V1(
                nameof(AuthenticatorKey.Password),
                null,
                failureState.Value.FailedAttemptCount,
                failureState.Value.LastFailedAtUtc,
                failureState.Value.RecentAttemptTimestamps,
                failureState.Value.LockoutCount),
            AuthenticatorKey.Totp totp => new AuthenticatorFailureStateDso.V1(
                nameof(AuthenticatorKey.Totp),
                totp.Name.ToString(),
                failureState.Value.FailedAttemptCount,
                failureState.Value.LastFailedAtUtc,
                failureState.Value.RecentAttemptTimestamps,
                failureState.Value.LockoutCount),
            AuthenticatorKey.RecoveryCode => new AuthenticatorFailureStateDso.V1(
                nameof(AuthenticatorKey.RecoveryCode),
                null,
                failureState.Value.FailedAttemptCount,
                failureState.Value.LastFailedAtUtc,
                failureState.Value.RecentAttemptTimestamps,
                failureState.Value.LockoutCount),
            _ => throw new InvalidOperationException($"Unexpected authenticator key type: {failureState.Key.GetType().Name}")
        };

    private static KeyValuePair<AuthenticatorKey, AuthenticatorFailureState> ToFailureState(
        AuthenticatorFailureStateDso.V1 dso)
    {
        AuthenticatorKey key = dso.AuthenticatorType switch
        {
            nameof(AuthenticatorKey.Password) => new AuthenticatorKey.Password(),
            nameof(AuthenticatorKey.Totp) => new AuthenticatorKey.Totp(
                TotpAuthenticatorName.Load(
                    !string.IsNullOrWhiteSpace(dso.AuthenticatorId)
                        ? dso.AuthenticatorId
                        : throw new InvalidOperationException(
                            $"AuthenticatorId is required when AuthenticatorType is {nameof(AuthenticatorKey.Totp)}."))),
            nameof(AuthenticatorKey.RecoveryCode) => new AuthenticatorKey.RecoveryCode(),
            _ => throw new InvalidOperationException($"Unexpected authenticator type: {dso.AuthenticatorType}")
        };

        return new KeyValuePair<AuthenticatorKey, AuthenticatorFailureState>(
            key,
            AuthenticatorFailureState.Load(dso.FailedAttemptCount, dso.LastFailedAtUtc, dso.RecentAttemptTimestamps, dso.LockoutCount));
    }

    private TotpAuthenticatorDso.V1 ToDso(TotpAuthenticator vo)
    {
        var key = vo.Key.Bytes.ToArray();
        var storedKey = _options.ProtectTotpKeys
            ? _dataProtector
                .CreateProtector(nameof(TotpAuthenticator))
                .CreateProtector(nameof(TotpAuthenticator.Key))
                .Protect(key)
            : key;

        return new TotpAuthenticatorDso.V1(
            vo.Name.Value, Convert.ToBase64String(storedKey), vo.LastSuccessfulTimeStep);
    }

    private TotpAuthenticator ToValueObject(TotpAuthenticatorDso.V1 dso)
    {
        var storedKey = Convert.FromBase64String(dso.Key);
        var key = _options.ProtectTotpKeys
            ? _dataProtector
                .CreateProtector(nameof(TotpAuthenticator))
                .CreateProtector(nameof(TotpAuthenticator.Key))
                .Unprotect(storedKey)
            : storedKey;

        return TotpAuthenticator.Load(
            TotpAuthenticatorName.Load(dso.Name), PlainBytesTotpKey.Load(key), dso.LastSuccessfulTimeStep);
    }

    private static List<DataStorageKey> GetJsonKeys(UserAuthenticators authenticators)
    {
        List<DataStorageKey> keys =
        [
            .. authenticators.OtpAddresses.Select(a => DataStorageKey.Create(OtpAddressDskV1.Create(a))),
            .. authenticators.ExternalAuthenticators.Select(a => DataStorageKey.Create(ExternalAuthenticatorDskV1.Create(a))),
            .. authenticators.PasskeyCredentials.Values.Select(c => DataStorageKey.Create(PasskeyCredentialIdDskV1.Create(c.CredentialId)))
        ];

        if (authenticators.UserName is not null)
        {
            keys.Add(DataStorageKey.Create(UserNameDskV1.Create(authenticators.UserName.Value)));
        }

        return keys;
    }

    internal sealed class Options
    {
        internal bool ProtectTotpKeys { get; set; } = true;
    }
}
