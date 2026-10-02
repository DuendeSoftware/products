// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services.KeyManagement;

internal static partial class Log
{
    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(ErrorReadingFileFileName),
        Message = "Error reading file: {FileName}")]
    internal static partial void ErrorReadingFileFileName(this ILogger logger, System.Exception exception, object fileName);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(ErrorDeletingFileFilePath),
        Message = "Error deleting file: {FilePath}")]
    internal static partial void ErrorDeletingFileFilePath(this ILogger logger, System.Exception exception, object filePath);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(GettingTheCurrentKey),
        Message = "Getting the current key.")]
    internal static partial void GettingTheCurrentKey(this ILogger logger);

    [LoggerMessage(
        LogLevel.Information,
        EventName = nameof(ActiveSigningKeyFoundWithKidKidFor),
        Message = "Active signing key found with kid {Kid} for alg {Alg}. Expires in {KeyExpiration}. Retires in {KeyRetirement}")]
    internal static partial void ActiveSigningKeyFoundWithKidKidFor(this ILogger logger, object kid, object alg, object keyExpiration, object keyRetirement);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(GettingAllTheKeys),
        Message = "Getting all the keys.")]
    internal static partial void GettingAllTheKeys(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(NotAllSigningKeysCurrentInCacheReloading),
        Message = "Not all signing keys current in cache, reloading keys from database.")]
    internal static partial void NotAllSigningKeysCurrentInCacheReloading(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(KeyRotationRequiredReloadingKeysFromDatabase),
        Message = "Key rotation required, reloading keys from database.")]
    internal static partial void KeyRotationRequiredReloadingKeysFromDatabase(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(EnteringNewKeyLock),
        Message = "Entering new key lock.")]
    internal static partial void EnteringNewKeyLock(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(NoActiveKeysNewKeyCreationRequired),
        Message = "No active keys; new key creation required.")]
    internal static partial void NoActiveKeysNewKeyCreationRequired(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(ApproachingKeyRetirementNewKeyCreationRequired),
        Message = "Approaching key retirement; new key creation required.")]
    internal static partial void ApproachingKeyRetirementNewKeyCreationRequired(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(AnotherServerCreatedNewKey),
        Message = "Another server created new key.")]
    internal static partial void AnotherServerCreatedNewKey(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(AnotherThreadCreatedNewKey),
        Message = "Another thread created new key.")]
    internal static partial void AnotherThreadCreatedNewKey(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(ReleasingNewKeyLock),
        Message = "Releasing new key lock.")]
    internal static partial void ReleasingNewKeyLock(this ILogger logger);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(FailedToCreateAndThenLoadNewKeys),
        Message = "Failed to create and then load new keys.")]
    internal static partial void FailedToCreateAndThenLoadNewKeys(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(KeyRotationNotRequiredForAlgAlgNew),
        Message = "Key rotation not required for alg {Alg}; New key expected to be created in {KeyRotiation}")]
    internal static partial void KeyRotationNotRequiredForAlgAlgNew(this ILogger logger, object alg, object keyRotiation);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(KeyRotationRequiredNowForAlgAlg),
        Message = "Key rotation required now for alg {Alg}.")]
    internal static partial void KeyRotationRequiredNowForAlgAlg(this ILogger logger, object alg);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CreatingNewKey),
        Message = "Creating new key.")]
    internal static partial void CreatingNewKey(this ILogger logger);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(CreatedAndStoredNewKeyWithKidKid),
        Message = "Created and stored new key with kid {Kid}.")]
    internal static partial void CreatedAndStoredNewKeyWithKidKid(this ILogger logger, object kid);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CacheHitWhenLoadingAllKeys),
        Message = "Cache hit when loading all keys.")]
    internal static partial void CacheHitWhenLoadingAllKeys(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CacheMissWhenLoadingAllKeys),
        Message = "Cache miss when loading all keys.")]
    internal static partial void CacheMissWhenLoadingAllKeys(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(FilteredRetiredKeysFromStoreKids),
        Message = "Filtered retired keys from store: {Kids}")]
    internal static partial void FilteredRetiredKeysFromStoreKids(this ILogger logger, object kids);

    [LoggerMessage(
        LogLevel.Debug,
        EventName = nameof(DeletingRetiredKeysFromStoreKids),
        Message = "Deleting retired keys from store: {Kids}")]
    internal static partial void DeletingRetiredKeysFromStoreKids(this ILogger logger, object kids);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CachingKeysWithInitializationKeyCacheDurationForInitializationKeyCacheDuration),
        Message = "Caching keys with InitializationKeyCacheDuration for {InitializationKeyCacheDuration}")]
    internal static partial void CachingKeysWithInitializationKeyCacheDurationForInitializationKeyCacheDuration(this ILogger logger, object initializationKeyCacheDuration);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CachingKeysWithKeyCacheDurationForKeyCacheDuration),
        Message = "Caching keys with KeyCacheDuration for {KeyCacheDuration}")]
    internal static partial void CachingKeysWithKeyCacheDurationForKeyCacheDuration(this ILogger logger, object keyCacheDuration);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(LoadingKeysFromStore),
        Message = "Loading keys from store.")]
    internal static partial void LoadingKeysFromStore(this ILogger logger);

    [LoggerMessage(
        LogLevel.Warning,
        EventName = nameof(KeyWithKidKidFailedToUnprotect),
        Message = "Key with kid {Kid} failed to unprotect.")]
    internal static partial void KeyWithKidKidFailedToUnprotect(this ILogger logger, object kid);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(ErrorUnprotectingTheIdentityServerSigningKeyWithKid),
        Message = "Error unprotecting the IdentityServer signing key with kid {Kid}. This is likely due to the ASP.NET Core data protection key that was used to protect it is not available. This could occur because data protection has not been configured properly for your load balanced environment, or the IdentityServer signing key store was populated with keys from a different environment with different ASP.NET Core data protection keys. Once you have corrected the problem and if you keep getting this error then it is safe to delete the specific IdentityServer signing key with that kid.")]
    internal static partial void ErrorUnprotectingTheIdentityServerSigningKeyWithKid(this ILogger logger, System.Exception exception, object kid);

    [LoggerMessage(
        LogLevel.Error,
        EventName = nameof(ErrorLoadingKeyWithKidKid),
        Message = "Error loading key with kid {Kid}.")]
    internal static partial void ErrorLoadingKeyWithKidKid(this ILogger logger, System.Exception exception, object kid);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(LoadedKeysFromStoreKids),
        Message = "Loaded keys from store: {Kids}")]
    internal static partial void LoadedKeysFromStoreKids(this ILogger logger, object kids);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(RemainingKeysAfterFilterKids),
        Message = "Remaining keys after filter: {Kids}")]
    internal static partial void RemainingKeysAfterFilterKids(this ILogger logger, object kids);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(KeysWithAllowedAlgFromStoreKids),
        Message = "Keys with allowed alg from store: {Kids}")]
    internal static partial void KeysWithAllowedAlgFromStoreKids(this ILogger logger, object kids);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(KeysSuccessfullyReturnedFromStore),
        Message = "Keys successfully returned from store.")]
    internal static partial void KeysSuccessfullyReturnedFromStore(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(NoKeysReturnedFromStore),
        Message = "No keys returned from store.")]
    internal static partial void NoKeysReturnedFromStore(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(AllKeysAreNewDelayingBeforeReloadingKeys),
        Message = "All keys are new; delaying before reloading keys from store by InitializationSynchronizationDelay for {InitializationSynchronizationDelay}.")]
    internal static partial void AllKeysAreNewDelayingBeforeReloadingKeys(this ILogger logger, object initializationSynchronizationDelay);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(AllKeysAreNewReloadingKeysFromStore),
        Message = "All keys are new; reloading keys from store.")]
    internal static partial void AllKeysAreNewReloadingKeysFromStore(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(LookingForActiveSigningKeys),
        Message = "Looking for active signing keys.")]
    internal static partial void LookingForActiveSigningKeys(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(LookingForAnActiveSigningKeyForAlg),
        Message = "Looking for an active signing key for alg {Alg}.")]
    internal static partial void LookingForAnActiveSigningKeyForAlg(this ILogger logger, object alg);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(FoundActiveSigningKeyForAlgAlgWith),
        Message = "Found active signing key for alg {Alg} with kid {Kid}.")]
    internal static partial void FoundActiveSigningKeyForAlgAlgWith(this ILogger logger, object alg, object kid);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(FailedToFindActiveSigningKeyForAlg),
        Message = "Failed to find active signing key for alg {Alg}.")]
    internal static partial void FailedToFindActiveSigningKeyForAlg(this ILogger logger, object alg);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(NoActiveSigningKeyFoundRespectingTheActivation),
        Message = "No active signing key found (respecting the activation delay).")]
    internal static partial void NoActiveSigningKeyFoundRespectingTheActivation(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(NoActiveSigningKeyFoundIgnoringTheActivation),
        Message = "No active signing key found (ignoring the activation delay).")]
    internal static partial void NoActiveSigningKeyFoundIgnoringTheActivation(this ILogger logger);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(ActiveSigningKeyFoundDelayWithKidKid),
        Message = "Active signing key found {Delay} with kid: {Kid}.")]
    internal static partial void ActiveSigningKeyFoundDelayWithKidKid(this ILogger logger, object delay, object kid);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(KeyKidSigningAlgorithmAlgNotAllowedBy),
        Message = "Key {Kid} signing algorithm {Alg} not allowed by server options.")]
    internal static partial void KeyKidSigningAlgorithmAlgNotAllowedBy(this ILogger logger, object kid, object alg);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(ServerConfiguredToWrapKeysInX509Certs),
        Message = "Server configured to wrap keys in X509 certs, but key {Kid} is not wrapped in cert.")]
    internal static partial void ServerConfiguredToWrapKeysInX509Certs(this ILogger logger, object kid);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CheckingIfKeyWithKidKidIsActive),
        Message = "Checking if key with kid {Kid} is active (respecting activation delay).")]
    internal static partial void CheckingIfKeyWithKidKidIsActive(this ILogger logger, object kid);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(CheckingIfKeyWithKidKidIsActive2),
        Message = "Checking if key with kid {Kid} is active (ignoring activation delay).")]
    internal static partial void CheckingIfKeyWithKidKidIsActive2(this ILogger logger, object kid);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(KeyWithKidKidIsInactiveTheCurrent),
        Message = "Key with kid {Kid} is inactive: the current time is prior to its activation delay.")]
    internal static partial void KeyWithKidKidIsInactiveTheCurrent(this ILogger logger, object kid);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(KeyWithKidKidIsInactiveTheCurrent2),
        Message = "Key with kid {Kid} is inactive: the current time is past its expiration.")]
    internal static partial void KeyWithKidKidIsInactiveTheCurrent2(this ILogger logger, object kid);

    [LoggerMessage(
        LogLevel.Trace,
        EventName = nameof(KeyWithKidKidIsActive),
        Message = "Key with kid {Kid} is active.")]
    internal static partial void KeyWithKidKidIsActive(this ILogger logger, object kid);
}
