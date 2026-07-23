// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

#nullable enable

using System.Text.Json;
using Duende.IdentityServer.Saml;

namespace Duende.IdentityServer.Stores.Storage.SamlSigninState;

/// <summary>
/// Internal JSON-based implementation of <see cref="ISamlSigninStateSerializer"/>.
/// Registered via TryAdd so it yields to the EF public version when both are present.
/// </summary>
#pragma warning disable CA1812 // Avoid uninstantiated internal classes
internal sealed class JsonSamlSigninStateSerializer : ISamlSigninStateSerializer
{
    /// <inheritdoc/>
    public string Serialize(SamlAuthenticationState state) =>
        JsonSerializer.Serialize(state, JsonSerializerOptions.Default);

    /// <inheritdoc/>
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
