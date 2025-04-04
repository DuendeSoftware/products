// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Duende.Bff.Configuration;

/// <summary>
/// Delegate that determines if the anti forgery check should be disabled for a request.
/// </summary>
/// <param name="context"></param>
/// <returns></returns>
public delegate bool DisableAntiForgeryCheck(HttpContext context);
