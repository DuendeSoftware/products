// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// global/shared
[assembly: SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Consistent with the IdentityServer APIs")]
[assembly: SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Consistent with the IdentityServer APIs")]
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task", Justification = "No need for ConfigureAwait in ASP.NET Core application code, as there is no SynchronizationContext.")]

// page specific
[assembly: SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "TestUsers are not designed to be extended", Scope = "member", Target = "~P:IdentityServerHost.TestUsers.Users")]
[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "ExternalProvider is nested by design", Scope = "type", Target = "~T:IdentityServerHost.Pages.Login.ViewModel.ExternalProvider")]
[assembly: SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "This namespace is just for organization, and won't be referenced elsewhere", Scope = "namespace", Target = "~N:IdentityServerHost.Pages.Error")]
[assembly: SuppressMessage("Naming", "CA1724:Type names should not match namespaces", Justification = "Namespaces of pages are not likely to be used elsewhere, so there is little chance of confusion", Scope = "type", Target = "~T:IdentityServerHost.Pages.Ciba.Consent")]
[assembly: SuppressMessage("Naming", "CA1724:Type names should not match namespaces", Justification = "Namespaces of pages are not likely to be used elsewhere, so there is little chance of confusion", Scope = "type", Target = "~T:IdentityServerHost.Pages.Extensions")]
[assembly: SuppressMessage("Performance", "CA1805:Do not initialize unnecessarily", Justification = "This is for clarity and consistency with the surrounding code", Scope = "member", Target = "~F:IdentityServerHost.Pages.Logout.LogoutOptions.AutomaticRedirectAfterSignOut")]
