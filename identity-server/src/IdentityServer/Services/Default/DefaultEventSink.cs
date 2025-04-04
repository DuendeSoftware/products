// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


using Duende.IdentityServer.Events;
using Microsoft.Extensions.Logging;

namespace Duende.IdentityServer.Services;

/// <summary>
/// Default implementation of the event service. Write events raised to the log.
/// </summary>
public class DefaultEventSink : IEventSink
{
    /// <summary>
    /// The logger
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultEventSink"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public DefaultEventSink(ILogger<DefaultEventService> logger) => _logger = logger;

    /// <summary>
    /// Raises the specified event.
    /// </summary>
    /// <param name="evt">The event.</param>
    /// <exception cref="System.ArgumentNullException">evt</exception>
    public virtual Task PersistAsync(Event evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        _logger.LogInformation("{@event}", evt);

        return Task.CompletedTask;
    }
}
