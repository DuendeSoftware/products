// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using System.Diagnostics;
using Duende.IdentityServer.Configuration;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Http;

namespace Duende.IdentityServer.Events;

/// <summary>
/// The default event service
/// </summary>
/// <seealso cref="IEventService" />
public class DefaultEventService : IEventService
{
    /// <summary>
    /// The options
    /// </summary>
    protected readonly IdentityServerOptions Options;

    /// <summary>
    /// The context
    /// </summary>
    protected readonly IHttpContextAccessor Context;

    /// <summary>
    /// The sink
    /// </summary>
    protected readonly IEventSink Sink;

    /// <summary>
    /// The time provider
    /// </summary>
    protected readonly TimeProvider TimeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultEventService"/> class.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="context">The context.</param>
    /// <param name="sink">The sink.</param>
    /// <param name="timeProvider">The time provider.</param>
    public DefaultEventService(IdentityServerOptions options, IHttpContextAccessor context, IEventSink sink, TimeProvider timeProvider)
    {
        Options = options;
        Context = context;
        Sink = sink;
        TimeProvider = timeProvider;
    }

    /// <inheritdoc/>
    public async Task RaiseAsync(Event evt, Ct ct)
    {
        ArgumentNullException.ThrowIfNull(evt);

        if (CanRaiseEvent(evt))
        {
            await PrepareEventAsync(evt, ct);
            await Sink.PersistAsync(evt, ct);
        }
    }

    /// <summary>
    /// Indicates if the type of event will be persisted.
    /// </summary>
    /// <param name="evtType"></param>
    /// <returns></returns>
    /// <exception cref="System.ArgumentOutOfRangeException"></exception>
    public bool CanRaiseEventType(EventTypes evtType) =>
        evtType switch
        {
            EventTypes.Failure => Options.Events.RaiseFailureEvents,
            EventTypes.Information => Options.Events.RaiseInformationEvents,
            EventTypes.Success => Options.Events.RaiseSuccessEvents,
            EventTypes.Error => Options.Events.RaiseErrorEvents,
            _ => throw new ArgumentOutOfRangeException(nameof(evtType))
        };

    /// <summary>
    /// Determines whether this event would be persisted.
    /// </summary>
    /// <param name="evt">The evt.</param>
    /// <returns>
    ///   <c>true</c> if this event would be persisted; otherwise, <c>false</c>.
    /// </returns>
    protected virtual bool CanRaiseEvent(Event evt) => CanRaiseEventType(evt.EventType);

    /// <summary>
    /// Prepares the event.
    /// </summary>
    /// <param name="evt">The evt.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    protected virtual async Task PrepareEventAsync(Event evt, Ct ct)
    {
        evt.TimeStamp = TimeProvider.GetUtcNow().DateTime;
        using var process = Process.GetCurrentProcess();
        evt.ProcessId = process.Id;

        if (Context.HttpContext?.TraceIdentifier != null)
        {
            evt.ActivityId = Context.HttpContext.TraceIdentifier;
        }
        else
        {
            evt.ActivityId = "unknown";
        }

        if (Context.HttpContext?.Connection.LocalIpAddress != null)
        {
            evt.LocalIpAddress = Context.HttpContext.Connection.LocalIpAddress + ":" + Context.HttpContext.Connection.LocalPort;
        }
        else
        {
            evt.LocalIpAddress = "unknown";
        }

        if (Context.HttpContext?.Connection.RemoteIpAddress != null)
        {
            evt.RemoteIpAddress = Context.HttpContext.Connection.RemoteIpAddress.ToString();
        }
        else
        {
            evt.RemoteIpAddress = "unknown";
        }

        await evt.PrepareAsync();
    }
}
