// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.Bff.Endpoints;

namespace Duende.Bff.Configuration;

/// <summary>
/// This attribute indicates that the BFF middleware will ignore the antiforgery header checks.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class BffApiSkipAntiforgeryAttribute : Attribute, IBffApiSkipAntiForgery;
