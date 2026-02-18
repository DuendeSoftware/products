// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using Xunit.Sdk;
using Xunit.v3;

namespace Duende.Xunit.Playwright.Retries;

/// <summary>
/// Used to capture messages to potentially be forwarded later. Messages are forwarded by
/// disposing of the message bus.
/// </summary>
internal sealed class DelayedMessageBus(IMessageBus innerBus) : IMessageBus
{
    private readonly List<IMessageSinkMessage> _messages = [];

    public bool QueueMessage(IMessageSinkMessage message)
    {
        lock (_messages)
        {
            _messages.Add(message);
        }

        // No way to ask the inner bus if they want to cancel without sending them the message, so
        // we just go ahead and continue always.
        return true;
    }

    public void Dispose()
    {
        foreach (var message in _messages)
        {
            innerBus.QueueMessage(message);
        }
    }
}
