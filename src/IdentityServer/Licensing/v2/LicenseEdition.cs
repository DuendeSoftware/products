// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Licensing.v2;

/// <summary>
/// The editions of our license, which give access to different features.
/// </summary>
public enum LicenseEdition
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    Enterprise,
    Business,
    Starter,
    Community,
    Bff
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}