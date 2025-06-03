// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.DynamicFrontends;

/// <summary>
/// Represents the endpoint that retrieves the index html
/// </summary>
public interface IIndexHtmlClient
{
    Task<string?> GetIndexHtmlAsync(CT ct = default);
}
