// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.


namespace Duende.IdentityServer.Stores;

/// <summary>
/// Thrown by <see cref="ServerSideTicketStore"/> when the session store refuses a save because the session
/// was changed or removed after it was read. The session coordination service catches it and skips that extension.
/// </summary>
#pragma warning disable CA1032, CA1064 // Internal exception, only ever created by ServerSideTicketStore with a fixed message
internal sealed class ServerSideSessionConflictException()
    : Exception("The server-side session was changed or removed after it was read; the update was not saved.");
#pragma warning restore CA1032, CA1064
