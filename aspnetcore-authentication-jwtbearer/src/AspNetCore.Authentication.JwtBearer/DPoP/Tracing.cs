// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Duende.AspNetCore.Authentication.JwtBearer.DPoP;

internal static class Tracing
{
    private static readonly Version? AssemblyVersion = typeof(Tracing).Assembly.GetName().Version;

    public static string ServiceVersion =>
        AssemblyVersion != null ? $"{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{AssemblyVersion.Build}" :
            "Unknown Version";

    public static ActivitySource ActivitySource { get; } = new(
        "Duende.AspNetCore.Authentication.JwtBearer",
        ServiceVersion);
}
