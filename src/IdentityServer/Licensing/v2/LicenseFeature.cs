// Copyright (c) Duende Software. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.

#nullable enable
using System.ComponentModel;

namespace Duende.IdentityServer.Licensing.v2;

// TODO - Xmldoc
#pragma warning disable CS1591
public enum LicenseFeature
{
    [Description("key_management")]
    KeyManagement,

    [Description("par")]
    PAR,
 
    [Description("resource_isolation")]
    ResourceIsolation,
 
    [Description("dynamic_providers")]
    DynamicProviders,

    [Description("ciba")]
    CIBA,

    [Description("server_side_sessions")]
    ServerSideSessions,

    [Description("dpop")]
    DPoP,

    [Description("config_api")]
    DCR,
    
    [Description("isv")]
    ISV,
    
    [Description("redistribution")]
    Redistribution,
}

public static class LicenseFeatureExtensions
{
    internal static ulong ToFeatureMask(this LicenseFeature feature) => 1UL << (int) feature;

}