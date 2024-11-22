// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
namespace Duende.IdentityServer.Licensing.v2;

internal interface ILicenseAccessor
{
    License Current { get; }
}