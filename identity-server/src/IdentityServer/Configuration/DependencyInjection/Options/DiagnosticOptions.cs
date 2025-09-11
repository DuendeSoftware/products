// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Configuration;

/// <summary>
/// Options that control the way that diagnostic data is logged.
/// </summary>
public class DiagnosticOptions
{
    /// <summary>
    /// Frequency at which the diagnostic data is logged.
    /// The default value is 1 hour.
    /// </summary>
    public TimeSpan LogFrequency { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Max size of diagnostic data log message chunks in kilobytes.
    /// Defaults to 8160 bytes. 8 KB is a conservative limit for the max size of a log message that is imposed by
    /// some logging tools. We take 32 bytes less than that to allow for additional formatting of the log message.
    /// </summary>
    public int ChunkSize { get; set; } = (1024 * 8) - 32;
}
