// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DPoPApi;

public enum DPoPMode
{
    /// <summary>
    /// Only DPoP tokens will be accepted
    /// </summary>
    DPoPOnly,
    /// <summary>
    /// Both DPoP and Bearer tokens will be accepted
    /// </summary>
    DPoPAndBearer
}
