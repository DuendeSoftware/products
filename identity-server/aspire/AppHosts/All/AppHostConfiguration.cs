// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

// TODO: remove pragma?
#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace AppHosts.All;
#pragma warning restore IDE0130

public class AppHostConfiguration
{
    public string? IdentityHost { get; set; }
    public Dictionary<string, bool>? UseClients { get; set; }
    public bool RunDatabaseMigrations { get; set; }
    public Dictionary<string, bool>? UseApis { get; set; }
}
