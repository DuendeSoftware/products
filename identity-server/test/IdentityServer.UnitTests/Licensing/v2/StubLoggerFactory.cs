// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

using Microsoft.Extensions.Logging;

namespace IdentityServer.UnitTests.Licensing.V2;

public class StubLoggerFactory(ILogger logger) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName)
    {
        return logger;
    }
    
    public void Dispose()
    {
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }


}