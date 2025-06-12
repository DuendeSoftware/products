// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.Configuration;

/// <summary>
/// Marker interface for bff extension data. Each data extension can provide additional configuration or functionality.
/// This is added using extension methods, such as WithRemoteApis().
///
/// An example of this extension data is ProxyBffDataExtension, which contains the remote APIs for a frontend.
/// </summary>
internal interface IBffPlugin
{

}
