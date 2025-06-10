// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Licensing.V2.Diagnostics.DiagnosticEntries;

internal record RegisteredImplementationDetails(Type TInterface, List<Type> TDefaultImplementations);
