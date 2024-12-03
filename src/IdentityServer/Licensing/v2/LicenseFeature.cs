// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable
using System.ComponentModel;

namespace Duende.IdentityServer.Licensing.v2;

/// <summary>
/// The features of IdentityServer that can be enabled or disabled through the License.
/// </summary>
public enum LicenseFeature
{
    /// <summary>
    /// Automatic Key Management
    /// </summary>
    [Description("key_management")]
    KeyManagement,

    /// <summary>
    /// Pushed Authorization Requests
    /// </summary>
    [Description("par")]
    PAR,
 
    /// <summary>
    /// Resource Isolation
    /// </summary>
    [Description("resource_isolation")]
    ResourceIsolation,
 
    /// <summary>
    /// Dyanmic External Providers
    /// </summary>
    [Description("dynamic_providers")]
    DynamicProviders,

    /// <summary>
    /// Client Initiated Backchannel Authorization
    /// </summary>
    [Description("ciba")]
    CIBA,

    /// <summary>
    /// Server-Side Sessions
    /// </summary>
    [Description("server_side_sessions")]
    ServerSideSessions,

    /// <summary>
    /// Demonstrating Proof of Possesion
    /// </summary>
    [Description("dpop")]
    DPoP,

    /// <summary>
    /// Configuration API
    /// </summary>
    [Description("config_api")]
    DCR,
    
    /// <summary>
    /// ISV (same as Redistribution)
    /// </summary>
    [Description("isv")]
    ISV,
    
    /// <summary>
    /// Dedistribution
    /// </summary>
    [Description("redistribution")]
    Redistribution,
}

internal static class LicenseFeatureExtensions
{
    internal static ulong ToFeatureMask(this LicenseFeature feature) => 1UL << (int) feature;
}