// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


#nullable enable

using System.Threading.Tasks;
using Duende.IdentityServer.Stores;
using Duende.IdentityModel;

namespace Duende.IdentityServer.Services;

/// <inheritdoc />
public class PushedAuthorizationService : IPushedAuthorizationService
{
    private readonly IPushedAuthorizationSerializer _serializer;
    private readonly IPushedAuthorizationRequestStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="PushedAuthorizationService"/> class. 
    /// </summary>
    /// <param name="serializer">The pushed authorization serializer</param>
    /// <param name="store">The pushed authorization store</param>
    public PushedAuthorizationService(
        IPushedAuthorizationSerializer serializer,
        IPushedAuthorizationRequestStore store)
    {
        _serializer = serializer;
        _store = store;
    }

    /// <inheritdoc />
    public Task ConsumeAsync(string referenceValue)
    {
        return _store.ConsumeByHashAsync(referenceValue.ToSha256());
    }

    /// <inheritdoc />
    public async Task<DeserializedPushedAuthorizationRequest?> GetPushedAuthorizationRequestAsync(string referenceValue)
    {
        var par = await _store.GetByHashAsync(referenceValue.ToSha256());
        if (par == null)
        {
            return null;
        }
        var deserialized = _serializer.Deserialize(par.Parameters);
        return new DeserializedPushedAuthorizationRequest
        {
            ReferenceValue = referenceValue,
            PushedParameters = deserialized,
            ExpiresAtUtc = par.ExpiresAtUtc
        };
    }

    /// <inheritdoc />
    public Task StoreAsync(DeserializedPushedAuthorizationRequest request)
    {
        var protectedData = _serializer.Serialize(request.PushedParameters);
        return _store.StoreAsync(new Models.PushedAuthorizationRequest
        {
            ReferenceValueHash = request.ReferenceValue.ToSha256(),
            ExpiresAtUtc = request.ExpiresAtUtc,
            Parameters = protectedData
        });
    }
}
