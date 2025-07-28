// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Stores;
using Duende.IdentityServer.Stores.Serialization;
using Microsoft.Extensions.Logging;

namespace UnitTests.Stores.Default;

public class DefaultGrantStoreTests
{
    [Theory]
    [InlineData("D6F636DF19D11D1D3342A6CD445B67547AE2E3391D4587A189717F57DB5A6836-1", IdentityServerConstants.PersistedGrantTypes.AuthorizationCode, "625258A82BB3DB8CEFDF1A0E6C5562E5BD4157DFE4BF9F9B2612FAA2B48A6DEB")]
    [InlineData("B39C87365FEC0DA2DDC019BD3FCF8A4F742744333186630F627D5097DB6AC78E-1", IdentityServerConstants.PersistedGrantTypes.RefreshToken, "62B9BACAE58DAFAE996EC534C64B22F479BA4827FC30EF650533655814F49510")]
    [InlineData("2CF7C793822179510EAD431CB48803FAAFC82B4AFBD8C9C34C992EDDC446B836-1", IdentityServerConstants.PersistedGrantTypes.ReferenceToken, "8875DF1554A5B2C36DB749D7BCD08E237A9BAF8517F84DAD65E5352B05E5F57B")]
    [InlineData("D3029C0B55C009D92755AB716A378A3076E2002CCCD663932AB6281DE8E7BDF0-1", IdentityServerConstants.PersistedGrantTypes.BackChannelAuthenticationRequest, "D5343E6ED4C3C8779ED344EF012BDEE7DBA0B22D33672E9DB81645D8CC69BBDE")]
    [InlineData("mvc.code|1-1", IdentityServerConstants.PersistedGrantTypes.UserConsent, "3C30906A3EFFA714FF52C3563220EA6ADC7753150B58194D1C251B67384545F2")]
    public void GetHashKey_ReturnsExpectedHashKey(string key, string grantType, string expectedHashKey)
    {
        var store = new TestGrantStore<string>(grantType, null, null, null, null);

        var hashedKey = store.GetHashedKey(key);

        hashedKey.ShouldBe(expectedHashKey);
    }

    private class TestGrantStore<T>(
        string grantType,
        IPersistedGrantStore store,
        IPersistentGrantSerializer serializer,
        IHandleGenerationService handleGenerationService,
        ILogger logger)
        : DefaultGrantStore<T>(grantType, store, serializer, handleGenerationService, logger)
    {
        public new string GetHashedKey(string value) => base.GetHashedKey(value);
    }
}
