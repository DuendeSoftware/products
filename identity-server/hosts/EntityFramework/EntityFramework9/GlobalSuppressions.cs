// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// shared
[assembly: SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Main catches and logs all exceptions by design")]
[assembly: SuppressMessage("Naming", "CA1724:Type names should not match namespaces", Justification = "Resources is only used for initialization, so there is little chance of confusion", Scope = "type", Target = "~T:IdentityServerHost.Configuration.Resources")]
[assembly: SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "Maybe we'll do this someday, but right now it seems a dull chore", Scope = "module")]
