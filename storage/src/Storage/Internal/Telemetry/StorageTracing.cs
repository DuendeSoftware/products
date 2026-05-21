// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics;

namespace Duende.Storage.Internal.Telemetry;

/// <summary>
/// Constants and ActivitySources for tracing storage operations.
/// </summary>
internal static class StorageTracing
{
    private static readonly Version? AssemblyVersion = typeof(StorageTracing).Assembly.GetName().Version;

    public static string ServiceVersion { get; } = AssemblyVersion is { } v
        ? $"{v.Major}.{v.Minor}.{v.Build}"
        : "0.0.0";

    public static ActivitySource ActivitySource { get; } = new(SourceName, ServiceVersion);

    public const string SourceName = "Duende.Storage";
}
