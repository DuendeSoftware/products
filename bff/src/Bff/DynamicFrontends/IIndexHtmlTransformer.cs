// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.DynamicFrontends;

/// <summary>
/// Extension point to transform the index html before it is returned to the client.
///
/// You can use this to inject (frontend specific) custom HTML
/// </summary>
public interface IIndexHtmlTransformer
{
    Task<string?> Transform(string indexHtml, CT ct = default);
}
