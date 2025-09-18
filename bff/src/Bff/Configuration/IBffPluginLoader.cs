// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Duende.Bff.DynamicFrontends;

namespace Duende.Bff.Configuration;

/// <summary>
/// Different assemblies of the BFF, such as BFF Yarp can provide their own extensibility model.
/// For example, the remote api configuration should NOT be in the BFF assembly, but in the BFF Yarp assembly.
///
/// This plugin mechanism allows the BFF to load these extensions at runtime, so that the BFF can be used with different plugins.
/// 
/// </summary>
internal interface IBffPluginLoader
{
    /// <summary>
    /// Loads a data extension for a specific frontend.
    /// </summary>
    IBffPlugin? LoadExtension(BffFrontendName name);
}
