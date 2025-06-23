// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Hosts.Bff.Performance.Services;

public class ApiSettings
{
    public required Uri ApiUrl { get; set; }
    public required Uri IdentityServerUrl { get; set; }
}
