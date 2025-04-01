// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Bff.Configuration;

/// <summary>
/// Enum representing the style of response from the ~/bff/user endpoint when the user is anonymous.
/// </summary>
public enum AnonymousSessionResponse
{
    /// <summary>
    /// 401 response with empty body
    /// </summary>
    Response401,
    /// <summary>
    /// 200 response with "null" as the body
    /// </summary>
    Response200
}
