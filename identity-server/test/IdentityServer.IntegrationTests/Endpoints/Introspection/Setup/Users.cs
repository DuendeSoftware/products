// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Test;

namespace IntegrationTests.Endpoints.Introspection.Setup;

public static class Users
{
    public static List<TestUser> Get() => new List<TestUser>
        {
            new TestUser
            {
                SubjectId = "1",
                Username = "bob",
                Password = "bob"
            }
        };
}
