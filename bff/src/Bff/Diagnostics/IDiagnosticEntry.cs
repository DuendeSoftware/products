// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;

namespace Duende.Bff.Diagnostics;

internal interface IDiagnosticEntry
{
    public void Write(DiagnosticContext context, Utf8JsonWriter writer);
}
