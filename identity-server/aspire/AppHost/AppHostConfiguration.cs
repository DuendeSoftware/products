// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace aspire.orchestrator.AppHost;

public class AppHostConfiguration
{
    public string? IdentityHost { get; set; }
    public Dictionary<string, bool>? UseClients { get; set; }
    public bool RunDatabaseMigrations { get; set; }
    public Dictionary<string, bool>? UseApis { get; set; }
}
