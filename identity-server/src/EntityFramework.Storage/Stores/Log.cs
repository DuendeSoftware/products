// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.EntityFramework.Stores;

internal static partial class Log
{
    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{ClientId} found in database: {ClientIdFound}")]
    internal static partial void ValueFoundInDatabaseValue(ILogger logger, object clientId, bool clientIdFound);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Retrieved {ClientCount} clients for enumeration")]
    internal static partial void RetrievedValueClientsForEnumeration(ILogger logger, int clientCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{UserCode} found in database: {UserCodeFound}")]
    internal static partial void ValueFoundInDatabaseValue2(ILogger logger, object userCode, bool userCodeFound);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{DeviceCode} found in database: {DeviceCodeFound}")]
    internal static partial void ValueFoundInDatabaseValue3(ILogger logger, object deviceCode, bool deviceCodeFound);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "{UserCode} not found in database")]
    internal static partial void ValueNotFoundInDatabase(ILogger logger, object userCode);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{UserCode} found in database")]
    internal static partial void ValueFoundInDatabase(ILogger logger, object userCode);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "exception updating {UserCode} user code in database: {Error}")]
    internal static partial void ExceptionUpdatingValueUserCodeInDatabaseValue(ILogger logger, object userCode, object error);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "removing {DeviceCode} device code from database")]
    internal static partial void RemovingValueDeviceCodeFromDatabase(ILogger logger, object deviceCode);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "exception removing {DeviceCode} device code from database: {Error}")]
    internal static partial void ExceptionRemovingValueDeviceCodeFromDatabaseValue(ILogger logger, object deviceCode, object error);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "no {DeviceCode} device code found in database")]
    internal static partial void NoValueDeviceCodeFoundInDatabase(ILogger logger, object deviceCode);

    [LoggerMessage(EventId = 0, Level = LogLevel.Error, Message = "Identity provider record found in database, but mapping failed for scheme {Scheme} and protocol type {Protocol}")]
    internal static partial void IdentityProviderRecordFoundInDatabaseButMapping(ILogger logger, object scheme, object protocol);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{PersistedGrantKey} not found in database")]
    internal static partial void ValueNotFoundInDatabase2(ILogger logger, object persistedGrantKey);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{PersistedGrantKey} found in database")]
    internal static partial void ValueFoundInDatabase2(ILogger logger, object persistedGrantKey);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "exception updating {PersistedGrantKey} persisted grant in database: {Error}")]
    internal static partial void ExceptionUpdatingValuePersistedGrantInDatabaseValue(ILogger logger, object persistedGrantKey, object error);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{PersistedGrantKey} found in database: {PersistedGrantKeyFound}")]
    internal static partial void ValueFoundInDatabaseValue4(ILogger logger, object persistedGrantKey, bool persistedGrantKeyFound);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{PersistedGrantCount} persisted grants found for {@Filter}")]
    internal static partial void ValuePersistedGrantsFoundForValue(ILogger logger, int persistedGrantCount, object filter);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "removing {PersistedGrantKey} persisted grant from database")]
    internal static partial void RemovingValuePersistedGrantFromDatabase(ILogger logger, object persistedGrantKey);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "exception removing {PersistedGrantKey} persisted grant from database: {Error}")]
    internal static partial void ExceptionRemovingValuePersistedGrantFromDatabaseValue(ILogger logger, object persistedGrantKey, object error);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "no {PersistedGrantKey} persisted grant found in database")]
    internal static partial void NoValuePersistedGrantFoundInDatabase(ILogger logger, object persistedGrantKey);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "removing {PersistedGrantCount} persisted grants from database for {@Filter}")]
    internal static partial void RemovingValuePersistedGrantsFromDatabaseForValue(ILogger logger, int persistedGrantCount, object filter);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "removing {PersistedGrantCount} persisted grants from database for subject {@Filter}: {Error}")]
    internal static partial void RemovingValuePersistedGrantsFromDatabaseForSubject(ILogger logger, int persistedGrantCount, object filter, object error);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "removing {ReferenceValueHash} pushed authorization from database")]
    internal static partial void RemovingValuePushedAuthorizationFromDatabase(ILogger logger, object referenceValueHash);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "attempted to remove {ReferenceValueHash} pushed authorization request because it was consumed, but no records were actually deleted.")]
    internal static partial void AttemptedToRemoveValuePushedAuthorizationRequestBecause(ILogger logger, object referenceValueHash);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{ReferenceValueHash} pushed authorization found in database: {RequestUriFound}")]
    internal static partial void ValuePushedAuthorizationFoundInDatabaseValue(ILogger logger, object referenceValueHash, bool requestUriFound);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "exception updating {ReferenceValueHash} pushed authorization in database: {Error}")]
    internal static partial void ExceptionUpdatingValuePushedAuthorizationInDatabaseValue(ILogger logger, object referenceValueHash, object error);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Found {Apis} API resource in database")]
    internal static partial void FoundValueApiResourceInDatabase(ILogger logger, IEnumerable<string> apis);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Did not find {Apis} API resource in database")]
    internal static partial void DidNotFindValueApiResourceInDatabase(ILogger logger, IEnumerable<string> apis);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Found {Apis} API resources in database")]
    internal static partial void FoundValueApiResourcesInDatabase(ILogger logger, IEnumerable<string> apis);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Found {Scopes} identity scopes in database")]
    internal static partial void FoundValueIdentityScopesInDatabase(ILogger logger, IEnumerable<string> scopes);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Found {Scopes} scopes in database")]
    internal static partial void FoundValueScopesInDatabase(ILogger logger, IEnumerable<string> scopes);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Found {Scopes} as all scopes, and {Apis} as API resources")]
    internal static partial void FoundValueAsAllScopesAndValueAs(ILogger logger, IEnumerable<string> scopes, IEnumerable<string> apis);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "exception storing SAML logout session in database: {Error}")]
    internal static partial void ExceptionStoringSamlLogoutSessionInDatabase(ILogger logger, object error);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "stored SAML logout session {LogoutId} in database")]
    internal static partial void StoredSamlLogoutSessionValueInDatabase(ILogger logger, object logoutId);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "SAML logout session {LogoutId} found in database: {Found}")]
    internal static partial void SamlLogoutSessionValueFoundInDatabaseValue(ILogger logger, object logoutId, bool found);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Concurrency conflict recording SAML logout response for requestId {RequestId}, retrying (attempt {Attempt})")]
    internal static partial void ConcurrencyConflictRecordingSamlLogoutResponseForRequestid(ILogger logger, object requestId, int attempt);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to record SAML logout response for requestId {RequestId} after {MaxRetries} attempts due to concurrency conflicts")]
    internal static partial void FailedToRecordSamlLogoutResponseForRequestid(ILogger logger, object requestId, int maxRetries);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Failed to deserialize SAML logout session — skipping")]
    internal static partial void FailedToDeserializeSamlLogoutSessionSkipping(ILogger logger);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "SAML logout response issuer mismatch for requestId {RequestId}. Expected {ExpectedIssuer}, received {ActualIssuer}")]
    internal static partial void SamlLogoutResponseIssuerMismatchForRequestidValue(ILogger logger, object requestId, object expectedIssuer, object actualIssuer);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "recorded SAML logout response for requestId {RequestId} (success={Success})")]
    internal static partial void RecordedSamlLogoutResponseForRequestidValueSuccess(ILogger logger, object requestId, bool success);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "removed SAML logout session {LogoutId} from database")]
    internal static partial void RemovedSamlLogoutSessionValueFromDatabase(ILogger logger, object logoutId);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{EntityId} not found in database")]
    internal static partial void ValueNotFoundInDatabase3(ILogger logger, object entityId);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "{EntityId} found in database")]
    internal static partial void ValueFoundInDatabase3(ILogger logger, object entityId);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Retrieved {Count} SAML Service Providers for enumeration")]
    internal static partial void RetrievedValueSamlServiceProvidersForEnumeration(ILogger logger, int count);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "exception storing SAML signin state {StateId} in database: {Error}")]
    internal static partial void ExceptionStoringSamlSigninStateValueInDatabase(ILogger logger, Guid stateId, object error);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "stored SAML signin state {StateId} in database")]
    internal static partial void StoredSamlSigninStateValueInDatabase(ILogger logger, Guid stateId);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "SAML signin state {StateId} found in database: {Found}")]
    internal static partial void SamlSigninStateValueFoundInDatabaseValue(ILogger logger, Guid stateId, bool found);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "removed SAML signin state {StateId} from database")]
    internal static partial void RemovedSamlSigninStateValueFromDatabase(ILogger logger, Guid stateId);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "SAML signin state {StateId} not found or expired for update")]
    internal static partial void SamlSigninStateValueNotFoundOrExpired(ILogger logger, Guid stateId);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "updated SAML signin state {StateId} in database")]
    internal static partial void UpdatedSamlSigninStateValueInDatabase(ILogger logger, Guid stateId);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Created new server-side session {ServerSideSessionKey} in database")]
    internal static partial void CreatedNewServerSideSessionValueInDatabase(ILogger logger, object serverSideSessionKey);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Exception creating new server-side session in database: {Error}")]
    internal static partial void ExceptionCreatingNewServerSideSessionInDatabase(ILogger logger, object error);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Found server-side session {ServerSideSessionKey} in database: {ServerSideSessionKeyFound}")]
    internal static partial void FoundServerSideSessionValueInDatabaseValue(ILogger logger, object serverSideSessionKey, bool serverSideSessionKeyFound);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "No server-side session {ServerSideSessionKey} found in database. Update failed.")]
    internal static partial void NoServerSideSessionValueFoundInDatabase(ILogger logger, object serverSideSessionKey);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Updated server-side session {ServerSideSessionKey} in database")]
    internal static partial void UpdatedServerSideSessionValueInDatabase(ILogger logger, object serverSideSessionKey);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Exception updating existing server side session {ServerSideSessionKey} in database: {Error}")]
    internal static partial void ExceptionUpdatingExistingServerSideSessionValueIn(ILogger logger, object serverSideSessionKey, object error);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "No server side session {ServerSideSessionKey} found in database. Delete failed.")]
    internal static partial void NoServerSideSessionValueFoundInDatabase2(ILogger logger, object serverSideSessionKey);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Deleted server-side session {ServerSideSessionKey} in database")]
    internal static partial void DeletedServerSideSessionValueInDatabase(ILogger logger, object serverSideSessionKey);

    [LoggerMessage(EventId = 0, Level = LogLevel.Warning, Message = "Exception deleting server-side session {ServerSideSessionKey} in database: {Error}")]
    internal static partial void ExceptionDeletingServerSideSessionValueInDatabase(ILogger logger, object serverSideSessionKey, object error);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Found {ServerSideSessionCount} server-side sessions for {@Filter}")]
    internal static partial void FoundValueServerSideSessionsForValue(ILogger logger, int serverSideSessionCount, object filter);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Removed {ServerSideSessionCount} server-side sessions from database for {@Filter}")]
    internal static partial void RemovedValueServerSideSessionsFromDatabaseFor(ILogger logger, int serverSideSessionCount, object filter);

    [LoggerMessage(EventId = 0, Level = LogLevel.Information, Message = "Error removing {ServerSideSessionCount} server-side sessions from database for {@Filter}: {Error}")]
    internal static partial void ErrorRemovingValueServerSideSessionsFromDatabase(ILogger logger, int serverSideSessionCount, object filter, object error);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Found and removed {ServerSideSessionCount} expired server-side sessions")]
    internal static partial void FoundAndRemovedValueExpiredServerSideSessions(ILogger logger, int serverSideSessionCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Found {ServerSideSessionCount} server-side sessions in database")]
    internal static partial void FoundValueServerSideSessionsInDatabase(ILogger logger, int serverSideSessionCount);

    [LoggerMessage(EventId = 0, Level = LogLevel.Debug, Message = "Concurrency exception caught deleting key id {Kid}")]
    internal static partial void ConcurrencyExceptionCaughtDeletingKeyIdValue(ILogger logger, object kid);

}
