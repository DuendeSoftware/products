// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.IdentityServer.Hosting.OutboxProcessor;

/// <summary>
/// Marker interface indicating that the storage layer handles server-side session
/// expiration via the outbox processor mechanism. When registered, the existing
/// <c>ServerSideSessionCleanupHost</c> short-circuits to avoid race conditions
/// with the <see cref="OutboxProcessorHost"/>.
/// </summary>
internal interface IStorageBackedSessionsMarker;

/// <summary>
/// Concrete implementation of <see cref="IStorageBackedSessionsMarker"/> for DI registration.
/// </summary>
internal sealed class StorageBackedSessionsMarker : IStorageBackedSessionsMarker;
