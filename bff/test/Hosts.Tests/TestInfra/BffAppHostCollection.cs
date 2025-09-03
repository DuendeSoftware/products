// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Hosts.Tests.TestInfra;

[CollectionDefinition(CollectionName)]
public class BffAppHostCollection : ICollectionFixture<BffHostTestFixture>
{
    public const string CollectionName = "apphost collection";
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
