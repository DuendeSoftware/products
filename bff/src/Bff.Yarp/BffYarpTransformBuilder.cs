// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Yarp.ReverseProxy.Transforms.Builder;

namespace Duende.Bff.Yarp;

/// <summary>
/// Delegate for pipeline transformers. 
/// </summary>
/// <param name="localPath">The local path that should be proxied. This path will be removed from the proxied request. </param>
/// <param name="context">The transform builder context</param>
public delegate void BffYarpTransformBuilder(string localPath, TransformBuilderContext context);
