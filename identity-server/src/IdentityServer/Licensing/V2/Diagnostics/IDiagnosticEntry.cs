// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace Duende.IdentityServer.Licensing.V2.Diagnostics;

internal interface IDiagnosticEntry
{
    Task WriteAsync(Utf8JsonWriter writer);
}
