// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.ComponentModel;

namespace Duende.IdentityServer.Licensing.v2;

/// <summary>
/// The features of IdentityServer that can be enabled or disabled through the License.
/// </summary>
public enum LicenseFeature : ulong
{
    /// <summary>
    /// Automatic Key Management
    /// </summary>
    [Description("key_management")]
    KeyManagement = 1,

    /// <summary>
    /// Pushed Authorization Requests
    /// </summary>
    [Description("par")]
    PAR = 2,
 
    /// <summary>
    /// Resource Isolation
    /// </summary>
    [Description("resource_isolation")]
    ResourceIsolation = 4,
 
    /// <summary>
    /// Dyanmic External Providers
    /// </summary>
    [Description("dynamic_providers")]
    DynamicProviders = 8,

    /// <summary>
    /// Client Initiated Backchannel Authorization
    /// </summary>
    [Description("ciba")]
    CIBA = 16,

    /// <summary>
    /// Server-Side Sessions
    /// </summary>
    [Description("server_side_sessions")]
    ServerSideSessions = 32,

    /// <summary>
    /// Demonstrating Proof of Possesion
    /// </summary>
    [Description("dpop")]
    DPoP = 64,

    /// <summary>
    /// Configuration API
    /// </summary>
    [Description("config_api")]
    DCR = 128,
    
    /// <summary>
    /// ISV (same as Redistribution)
    /// </summary>
    [Description("isv")]
    ISV = 256,
    
    /// <summary>
    /// Dedistribution
    /// </summary>
    [Description("redistribution")]
    Redistribution = 512,
}
