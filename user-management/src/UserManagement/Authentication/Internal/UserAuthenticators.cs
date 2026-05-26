// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.UserManagement.Authentication.External;
using Duende.UserManagement.Authentication.Otp;
using Duende.UserManagement.Authentication.Passkeys;
using Duende.UserManagement.Authentication.Passkeys.Internal;
using Duende.UserManagement.Authentication.Passwords;
using Duende.UserManagement.Authentication.RecoveryCodes;
using Duende.UserManagement.Authentication.Totp;
using Duende.UserManagement.Authentication.Totp.Internal;

namespace Duende.UserManagement.Authentication.Internal;

#pragma warning disable CS1573 // Parameter 'parameter' has no matching param tag in the XML comment for 'parameter' (but other parameters do)
internal sealed class UserAuthenticators
{
    private readonly HashSet<OtpAddress> _otpAddresses;
    private readonly HashSet<ExternalAuthenticator> _externalAuthenticators;
    private readonly Dictionary<TotpAuthenticatorName, TotpAuthenticator> _totpAuthenticators;
    private readonly Dictionary<PasskeyCredentialId, PasskeyCredential> _passkeyCredentials;
    private readonly Dictionary<AuthenticatorKey, AuthenticatorFailureState> _failureStates;
    private List<Pbkdf2HashedPassword> _recoveryCodes;
    private List<HashedPassword> _passwordHistory;

    private UserAuthenticators(
        UserAuthenticatorsId id,
        UserSubjectId subjectId,
        IEnumerable<OtpAddress> otpAddresses,
        IEnumerable<ExternalAuthenticator> externalAuthenticators,
        IEnumerable<TotpAuthenticator> totpAuthenticators,
        IEnumerable<Pbkdf2HashedPassword> recoveryCodes,
        UserName? userName,
        HashedPassword? hashedPassword,
        IEnumerable<PasskeyCredential> passkeyCredentials,
        IEnumerable<KeyValuePair<AuthenticatorKey, AuthenticatorFailureState>> failureStates,
        IEnumerable<HashedPassword> passwordHistory,
        DateTimeOffset? passwordSetAtUtc)
    {
        Id = id;
        SubjectId = subjectId;
        _otpAddresses = [.. otpAddresses];
        _externalAuthenticators = [.. externalAuthenticators];
        _totpAuthenticators = totpAuthenticators.ToDictionary(a => a.Name, a => a);
        _passkeyCredentials = passkeyCredentials.ToDictionary(c => c.CredentialId, c => c);
        _failureStates = failureStates.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        _recoveryCodes = [.. recoveryCodes];
        UserName = userName;
        HashedPassword = hashedPassword;
        _passwordHistory = [.. passwordHistory];
        PasswordSetAtUtc = passwordSetAtUtc;
    }

    internal UserAuthenticators(
        UserSubjectId subjectId,
        IEnumerable<OtpAddress> otpAddresses,
        IEnumerable<ExternalAuthenticator> externalAuthenticators) :
        this(
            UserAuthenticatorsId.New(),
            subjectId,
            otpAddresses,
            externalAuthenticators,
            [],
            [],
            null,
            null,
            [],
            [],
            [],
            null)
    {
    }

    internal UserAuthenticatorsId Id { get; }

    internal UserSubjectId SubjectId { get; }

    internal IReadOnlyCollection<OtpAddress> OtpAddresses => _otpAddresses;

    internal IReadOnlyDictionary<TotpAuthenticatorName, TotpAuthenticator> TotpAuthenticators => _totpAuthenticators;

    internal IReadOnlyCollection<ExternalAuthenticator> ExternalAuthenticators => _externalAuthenticators;

    internal IReadOnlyCollection<Pbkdf2HashedPassword> RecoveryCodes => _recoveryCodes;

    internal UserName? UserName { get; private set; }

    internal HashedPassword? HashedPassword { get; private set; }

    internal IReadOnlyList<HashedPassword> PasswordHistory => _passwordHistory;

    internal DateTimeOffset? PasswordSetAtUtc { get; private set; }

    internal IReadOnlyDictionary<PasskeyCredentialId, PasskeyCredential> PasskeyCredentials
        => _passkeyCredentials;

    internal IReadOnlyDictionary<AuthenticatorKey, AuthenticatorFailureState> FailureStates => _failureStates;

    internal void LoadHashedPassword(HashedPassword password) => HashedPassword = password;

    internal void Add(IEnumerable<OtpAddress> addresses) => _otpAddresses.UnionWith(addresses);

    internal void Remove(IEnumerable<OtpAddress> addresses) => _otpAddresses.ExceptWith(addresses);

    internal void Add(IEnumerable<ExternalAuthenticator> addresses) => _externalAuthenticators.UnionWith(addresses);

    internal void Remove(IEnumerable<ExternalAuthenticator> addresses) => _externalAuthenticators.ExceptWith(addresses);

    internal bool TryAdd(TotpAuthenticatorName totpAuthenticatorName, PlainBytesTotpKey totpKey, PlainTextTotp totp,
        TimeProvider timeProvider)
    {
        if (_totpAuthenticators.ContainsKey(totpAuthenticatorName))
        {
            return false;
        }

        if (!Totp.Internal.Totp.Validate(totpKey.Bytes, PlainTextTotp.Length, (ulong)timeProvider.GetUtcNow().ToUnixTimeSeconds(), totp.Value,
                null, out var successfulTimeStep))
        {
            return false;
        }

        _totpAuthenticators.Add(
            totpAuthenticatorName, new TotpAuthenticator(totpAuthenticatorName, totpKey, successfulTimeStep.Value));

        return true;
    }

    internal void LoadTotpAuthenticator(TotpAuthenticatorName name, PlainBytesTotpKey key) =>
        _totpAuthenticators.TryAdd(name, TotpAuthenticator.Load(name, key, 0));

    internal void LoadRecoveryCodes(IEnumerable<PlainTextRecoveryCode> codes) =>
        _recoveryCodes = codes.Select(code => Pbkdf2HashedPassword.From(code.Text)).ToList();

    internal void Remove(IEnumerable<TotpAuthenticatorName> totpAuthenticatorNames)
    {
        foreach (var name in totpAuthenticatorNames)
        {
            _ = _totpAuthenticators.Remove(name);
            _ = _failureStates.Remove(new AuthenticatorKey.Totp(name));
        }
    }

    internal IReadOnlyCollection<PlainTextRecoveryCode> CreateRecoveryCodes(int count)
    {
        count = Math.Clamp(count, 1, 50);
        var codes = Enumerable.Range(0, count).Select(_ => PlainTextRecoveryCode.New()).ToList();
        _recoveryCodes = codes.Select(code => Pbkdf2HashedPassword.From(code.Text)).ToList();
        return codes;
    }

    internal bool TrySetPassword(PlainTextPassword password, IPasswordHashAlgorithm algorithm, TimeProvider timeProvider)
    {
        if (HashedPassword is not null)
        {
            return false;
        }

        if (password.ValidatedForUserId != SubjectId)
        {
            return false;
        }

        HashedPassword = HashedPassword.From(password.Value, algorithm);
        PasswordSetAtUtc = timeProvider.GetUtcNow();
        return true;
    }

    internal bool TryChangePassword(NonValidatedPassword oldPassword, PlainTextPassword newPassword,
        IPasswordHashAlgorithm preferredAlgorithm, IReadOnlyList<IPasswordHashAlgorithm> allAlgorithms, int historyCount, TimeProvider timeProvider)
    {
        if (newPassword.ValidatedForUserId != SubjectId)
        {
            return false;
        }

        if (TryAuthenticate(this, oldPassword, preferredAlgorithm, allAlgorithms) is not { Authenticated: true })
        {
            return false;
        }

        if (MatchesPasswordHistory(newPassword.Value, historyCount, allAlgorithms))
        {
            return false;
        }

        PushPasswordHistory(historyCount);
        HashedPassword = HashedPassword.From(newPassword.Value, preferredAlgorithm);
        PasswordSetAtUtc = timeProvider.GetUtcNow();
        return true;
    }

    internal bool TryResetPassword(PlainTextPassword password, IPasswordHashAlgorithm algorithm,
        IReadOnlyList<IPasswordHashAlgorithm> allAlgorithms, int historyCount, TimeProvider timeProvider)
    {
        if (HashedPassword is null)
        {
            return false;
        }

        if (password.ValidatedForUserId != SubjectId)
        {
            return false;
        }

        if (MatchesPasswordHistory(password.Value, historyCount, allAlgorithms))
        {
            return false;
        }

        PushPasswordHistory(historyCount);
        HashedPassword = HashedPassword.From(password.Value, algorithm);
        PasswordSetAtUtc = timeProvider.GetUtcNow();
        return true;
    }

    internal bool MatchesPasswordHistory(string password, int historyCount, IReadOnlyList<IPasswordHashAlgorithm> allAlgorithms)
    {
        if (historyCount <= 0)
        {
            return false;
        }

        if (HashedPassword is { } current)
        {
            var currentAlgorithm = allAlgorithms.FirstOrDefault(a => a.AlgorithmId == current.AlgorithmId);
            if (currentAlgorithm is not null && currentAlgorithm.Verify(password, current.Data))
            {
                return true;
            }
        }

        foreach (var historicHash in _passwordHistory.Take(historyCount))
        {
            var algorithm = allAlgorithms.FirstOrDefault(a => a.AlgorithmId == historicHash.AlgorithmId);
            if (algorithm is not null && algorithm.Verify(password, historicHash.Data))
            {
                return true;
            }
        }

        return false;
    }

    private void PushPasswordHistory(int historyCount)
    {
        if (historyCount <= 0 || HashedPassword is null)
        {
            return;
        }

        _passwordHistory.Insert(0, HashedPassword);
        if (_passwordHistory.Count > historyCount)
        {
            _passwordHistory.RemoveRange(historyCount, _passwordHistory.Count - historyCount);
        }
    }

    internal void RehashPassword(string password, IPasswordHashAlgorithm preferredAlgorithm) =>
        HashedPassword = HashedPassword.From(password, preferredAlgorithm);

    internal void SetUserName(UserName userName) => UserName = userName;

    internal void RemoveUserName() => UserName = null;

    internal AuthenticatorFailureState GetFailureState(AuthenticatorKey key) =>
        _failureStates.TryGetValue(key, out var state)
            ? state
            : AuthenticatorFailureState.Load(0, null);

    internal AuthenticatorFailureState GetOrCreateFailureState(AuthenticatorKey key) =>
        _failureStates.TryGetValue(key, out var state)
            ? state
            : (_failureStates[key] = AuthenticatorFailureState.Load(0, null));

    internal void RecordFailedAttempt(AuthenticatorKey key, DateTimeOffset now, TimeSpan failureWindow) =>
        GetOrCreateFailureState(key).RecordFailure(now, failureWindow);

    internal void RecordFailedAttempt(AuthenticatorKey key, DateTimeOffset now, TimeSpan failureWindow, int maxFailedAttempts) =>
        GetOrCreateFailureState(key).RecordFailureWithLockout(now, failureWindow, maxFailedAttempts);

    internal void RecordAttempt(AuthenticatorKey key, DateTimeOffset now, TimeSpan velocityWindow) =>
        GetOrCreateFailureState(key).RecordAttempt(now, velocityWindow);

    internal void ResetFailedAttempts(AuthenticatorKey key)
    {
        if (_failureStates.TryGetValue(key, out var state))
        {
            state.ResetFailureCount();
        }
    }

    internal bool TryAdd(PasskeyCredential credential) =>
        _passkeyCredentials.Values.All(c => c.Name != credential.Name) &&
        _passkeyCredentials.TryAdd(credential.CredentialId, credential);

    internal bool TryRemove(PasskeyCredentialId credentialId) => _passkeyCredentials.Remove(credentialId);

    internal PasskeyCredential? TryGet(PasskeyCredentialId credentialId) =>
        _passkeyCredentials.GetValueOrDefault(credentialId);

    internal bool TryUpdateSignCount(PasskeyCredentialId credentialId, uint newSignCount)
    {
        if (!_passkeyCredentials.TryGetValue(credentialId, out var credential))
        {
            return false;
        }

        _passkeyCredentials[credentialId] = credential.WithUpdatedSignCount(newSignCount);
        return true;
    }

    internal bool TryUpdateBackedUp(PasskeyCredentialId credentialId, bool backedUp)
    {
        if (!_passkeyCredentials.TryGetValue(credentialId, out var credential))
        {
            return false;
        }

        _passkeyCredentials[credentialId] = credential.WithUpdatedBackedUp(backedUp);
        return true;
    }

    internal static bool TryAuthenticate(UserAuthenticators? user, TotpAuthenticatorName totpAuthenticatorName, PlainTextTotp totp,
        TimeProvider timeProvider)
    {
        // time consistency
        var dummyKey = PlainBytesTotpKey.New();
        if (user is null || !user._totpAuthenticators.TryGetValue(totpAuthenticatorName, out var authenticator))
        {
            _ = Totp.Internal.Totp.Validate(dummyKey.Bytes, PlainTextTotp.Length, (ulong)timeProvider.GetUtcNow().ToUnixTimeSeconds(),
                totp.Value, null, out _);
            return false;
        }

        if (!Totp.Internal.Totp.Validate(authenticator.Key.Bytes, PlainTextTotp.Length, (ulong)timeProvider.GetUtcNow().ToUnixTimeSeconds(),
                totp.Value, authenticator.LastSuccessfulTimeStep, out var successfulTimeStep))
        {
            return false;
        }

        user._totpAuthenticators[authenticator.Name] =
            authenticator with { LastSuccessfulTimeStep = successfulTimeStep.Value };

        return true;
    }

    internal static bool TryAuthenticate(UserAuthenticators? user, PlainTextRecoveryCode recoveryCode, int configuredCount)
    {
        // time consistency — always iterate at least configuredCount times to avoid
        // leaking how many recovery codes remain via timing differences.
        var dummyInputs = new Pbkdf2Inputs();
        var iterationCount = Math.Max(user?._recoveryCodes.Count ?? configuredCount, Math.Clamp(configuredCount, 1, 50));
        for (var i = 0; i < iterationCount; i++)
        {
            if (user is null || user._recoveryCodes.Count < i + 1)
            {
                var dummyMasterKey = Pbkdf2MasterKey.DeriveFrom(recoveryCode.Text, dummyInputs);
                _ = dummyMasterKey.Equals(dummyMasterKey);
                continue;
            }

            var userRecoveryCode = user._recoveryCodes[i];
            var masterKey = Pbkdf2MasterKey.DeriveFrom(recoveryCode.Text, userRecoveryCode.Inputs);
            if (!masterKey.Equals(userRecoveryCode.MasterKey))
            {
                continue;
            }

            user._recoveryCodes.RemoveAt(i);
            return true;
        }

        return false;
    }

    internal static PasswordAuthResult TryAuthenticate(UserAuthenticators? user, NonValidatedPassword password,
        IPasswordHashAlgorithm preferredAlgorithm, IEnumerable<IPasswordHashAlgorithm> allAlgorithms)
    {
        // time consistency — always perform a hash operation to prevent timing-based user enumeration
        if (user?.HashedPassword is null)
        {
            _ = preferredAlgorithm.Hash(password.Value);
            return new PasswordAuthResult(false, false);
        }

        var storedAlgorithmId = user.HashedPassword.AlgorithmId;
        var algorithm = allAlgorithms.FirstOrDefault(a => a.AlgorithmId == storedAlgorithmId);

        if (algorithm is null)
        {
            // Unknown algorithm — do dummy work and fail
            _ = preferredAlgorithm.Hash(password.Value);
            return new PasswordAuthResult(false, false);
        }

        var authenticated = algorithm.Verify(password.Value, user.HashedPassword.Data);
        var needsRehash = authenticated && (
            storedAlgorithmId != preferredAlgorithm.AlgorithmId ||
            preferredAlgorithm.NeedsRehash(user.HashedPassword.Data));
        return new PasswordAuthResult(authenticated, needsRehash);
    }

    internal static UserAuthenticators Load(
        UserAuthenticatorsId id,
        UserSubjectId subjectId,
        IEnumerable<OtpAddress> otpAddresses,
        IEnumerable<ExternalAuthenticator> externalAuthenticators,
        IEnumerable<TotpAuthenticator> totpAuthenticators,
        IEnumerable<Pbkdf2HashedPassword> recoveryCodes,
        UserName? userName,
        HashedPassword? hashedPassword,
        IEnumerable<PasskeyCredential> passkeyCredentials,
        IEnumerable<KeyValuePair<AuthenticatorKey, AuthenticatorFailureState>> failureStates,
        IEnumerable<HashedPassword> passwordHistory,
        DateTimeOffset? passwordSetAtUtc) =>
        new(
            id,
            subjectId,
            otpAddresses,
            externalAuthenticators,
            totpAuthenticators,
            recoveryCodes,
            userName,
            hashedPassword,
            passkeyCredentials,
            failureStates,
            passwordHistory,
            passwordSetAtUtc);
}
