// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.Bff.Endpoints;

namespace Duende.Bff.Configuration;

/// <summary>
/// This attribute indicates that the BFF middleware will not override the HTTP response status code.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class BffApiSkipResponseHandlingAttribute : Attribute, IBffApiSkipResponseHandling
{
}
