// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Text.Json;
using Duende.Bff.DynamicFrontends;

namespace Duende.Bff.Diagnostics.DiagnosticEntries;

internal class FrontendCountDiagnosticEntry(IFrontendCollection frontendCollection)
    : IDiagnosticEntry
{
    public void Write(DiagnosticContext context, Utf8JsonWriter writer) =>
        writer.WriteNumber("FrontendCount", frontendCollection.Count);
}
