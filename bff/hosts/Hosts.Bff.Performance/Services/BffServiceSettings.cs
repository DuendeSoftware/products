// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Hosts.ServiceDefaults;

namespace Hosts.Bff.Performance.Services;

public class BffServiceSettings
{
    public required string Uri { get; set; }

    public Uri ApiUrl { get; } = new Uri("https://localhost:5999");
}
