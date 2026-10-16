// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Duende.Spaces.Internal;

internal sealed class SpaceScopedResponseFeature(IHttpResponseFeature inner, ISpaceContextAccessor accessor) : IHttpResponseFeature
{
    public int StatusCode
    {
        get => inner.StatusCode;
        set => inner.StatusCode = value;
    }

    public string? ReasonPhrase
    {
        get => inner.ReasonPhrase;
        set => inner.ReasonPhrase = value;
    }

    public IHeaderDictionary Headers
    {
        get => inner.Headers;
        set => inner.Headers = value;
    }

    /// <summary>
    /// Obsolete because <see cref="IHttpResponseFeature.Body"/> is obsolete.
    /// </summary>
    [Obsolete("Use IHttpResponseBodyFeature.Stream instead.")]
    public Stream Body
    {
        get => inner.Body;
        set => inner.Body = value;
    }

    public bool HasStarted => inner.HasStarted;

    public void OnStarting(Func<object, Task> callback, object state) => inner.OnStarting(InCurrentSpace(callback), state);

    public void OnCompleted(Func<object, Task> callback, object state) => inner.OnCompleted(InCurrentSpace(callback), state);

    private Func<object, Task> InCurrentSpace(Func<object, Task> callback)
    {
        var spaceId = accessor.GetSpaceId();
        return async state =>
        {
            using (accessor.SetSpace(spaceId))
            {
                await callback(state);
            }
        };
    }
}
