// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Microsoft.Extensions.Logging;

namespace UnitTests.Common;

public static class TestLogger
{
    public static ILogger<T> Create<T>() => new LoggerFactory().CreateLogger<T>();
}
