// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Diagnostics;

namespace Duende.IdentityServer.Extensions;

internal static class StringExtensions
{
    [DebuggerStepThrough]
    public static bool IsMissing(this string value) => string.IsNullOrWhiteSpace(value);

    [DebuggerStepThrough]
    public static bool IsPresent(this string value) => !string.IsNullOrWhiteSpace(value);
}
