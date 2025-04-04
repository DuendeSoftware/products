// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace IdentityServer.UnitTests.Licensing.V2;

public class StubLoggerFactory(ILogger logger) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) => logger;

    public void Dispose()
    {
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }


}
