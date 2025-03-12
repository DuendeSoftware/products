// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff;

/// <summary>
/// Decorates a controller or action as a local BFF API endpoint
/// By default, this provides anti-forgery protection and response handling.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class BffApiAttribute : Attribute, IBffApiEndpoint
{
}
