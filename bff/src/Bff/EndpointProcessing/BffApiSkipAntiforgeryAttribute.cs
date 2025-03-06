// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

/// <summary>
/// This attribute indicates that the BFF middleware will ignore the antiforgery header checks.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class BffApiSkipAntiforgeryAttribute : Attribute, IBffApiSkipAntiforgery
{
}
