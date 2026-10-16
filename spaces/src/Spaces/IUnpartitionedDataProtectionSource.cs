// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.DataProtection;

namespace Duende.Spaces;

/// <summary>
/// Exposes the unpartitioned (global) data protection provider behind a space-partitioned
/// provider or protector.
/// </summary>
/// <remarks>
/// <para>
/// Warning: values protected through the unpartitioned provider are accepted in every space. Use it
/// only for data that is deliberately shared across spaces (for example signing keys). Never use the
/// same purpose chain both through <c>GetUnpartitioned()</c> and through the partitioned provider:
/// in the Default space the read fallback would accept the global value in both places. There is no
/// runtime check for this.
/// </para>
/// <para>
/// Wrappers around a partitioned provider or protector should implement this interface and forward
/// to the inner instance. Otherwise <c>GetUnpartitioned()</c> returns the wrapper itself, which is
/// still partitioned.
/// </para>
/// </remarks>
public interface IUnpartitionedDataProtectionSource
{
    /// <summary>
    /// Gets the unpartitioned provider. Values protected through it are accepted in every space.
    /// </summary>
    IDataProtectionProvider GetUnpartitioned();
}
