// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Linq;

namespace UnitTests.Common;

internal static class TestExtensions
{
    public static string Repeat(this string value, int count)
    {
        var parts = new string[count];
        return parts.Aggregate((x, y) => (x ?? value) + value);
    }
}