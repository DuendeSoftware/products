// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Stores;

namespace IdentityServer.UnitTests.Storage
{
    public class PersistedGrantFilterTests
    {
        [Fact]
        public void Validate_should_not_throw_if_any_property_is_not_null_or_empty()
        {
            var filter = new PersistedGrantFilter { ClientId = "client-id" };
            filter.Validate();

            filter = new PersistedGrantFilter { ClientIds = new[] { "client-id" } };
            filter.Validate();

            filter = new PersistedGrantFilter { Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken };
            filter.Validate();

            filter = new PersistedGrantFilter { Types = new[] { IdentityServerConstants.PersistedGrantTypes.RefreshToken } };
            filter.Validate();

            filter = new PersistedGrantFilter { SessionId = "sid" };
            filter.Validate();

            filter = new PersistedGrantFilter { SubjectId = "sub" };
            filter.Validate();

            filter = new PersistedGrantFilter
            {
                SessionId = "sid",
                SubjectId = "sub",
                ClientId = "client-id",
                Type = IdentityServerConstants.PersistedGrantTypes.RefreshToken
            };
            filter.Validate();

            // Nothing to assert, test passes if Validate didn't throw
        }

        [Fact]
        public void Validate_should_throw_if_all_properties_are_null_or_empty()
        {
            var filter = new PersistedGrantFilter();
            Assert.Throws<ArgumentException>(() => filter.Validate());
        }

    }
}
