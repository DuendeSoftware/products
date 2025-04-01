// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;

namespace Bff.Configuration;

/// <summary>
/// Delegate that defines if the default anti forgery check should be disabled for the current request
/// </summary>
/// <param name="context"></param>
/// <returns></returns>
public delegate bool DisableAntiForgeryCheck(HttpContext context);
