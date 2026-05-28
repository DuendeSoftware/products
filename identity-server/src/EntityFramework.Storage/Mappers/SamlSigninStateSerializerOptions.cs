// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Text.Json;
using Duende.IdentityServer.Saml;

namespace Duende.IdentityServer.EntityFramework.Mappers;

/// <summary>
/// Default implementation of <see cref="ISamlSigninStateSerializer"/> that uses
/// System.Text.Json for serializing <see cref="SamlAuthenticationState"/>.
/// No custom converters are needed because the state graph contains only simple
/// types that System.Text.Json handles natively.
/// </summary>
public sealed class DefaultSamlSigninStateSerializer : ISamlSigninStateSerializer
{
    /// <inheritdoc />
    public string Serialize(SamlAuthenticationState state) =>
        JsonSerializer.Serialize(state, JsonSerializerOptions.Default);

    /// <inheritdoc />
    public SamlAuthenticationState? Deserialize(string serializedState)
    {
        try
        {
            return JsonSerializer.Deserialize<SamlAuthenticationState>(serializedState, JsonSerializerOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
