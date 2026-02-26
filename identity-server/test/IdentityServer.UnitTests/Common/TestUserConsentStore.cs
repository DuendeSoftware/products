// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;

namespace UnitTests.Common;

public class TestUserConsentStore : IUserConsentStore
{
    private DefaultUserConsentStore _userConsentStore;
    private InMemoryPersistedGrantStore _grantStore = new InMemoryPersistedGrantStore();

    public TestUserConsentStore() => _userConsentStore = new DefaultUserConsentStore(
            _grantStore,
            new PersistentGrantSerializer(),
            new DefaultHandleGenerationService(),
            TestLogger.Create<DefaultUserConsentStore>());

    public Task StoreUserConsentAsync(Consent consent, Ct ct) => _userConsentStore.StoreUserConsentAsync(consent, ct);

    public Task<Consent> GetUserConsentAsync(string subjectId, string clientId, Ct ct) => _userConsentStore.GetUserConsentAsync(subjectId, clientId, ct);

    public Task RemoveUserConsentAsync(string subjectId, string clientId, Ct ct) => _userConsentStore.RemoveUserConsentAsync(subjectId, clientId, ct);
}
