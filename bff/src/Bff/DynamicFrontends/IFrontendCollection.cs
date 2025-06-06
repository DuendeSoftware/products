// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Bff.DynamicFrontends;

/// <summary>
/// Interface that allows adding, updating, and removing frontends in the BFF system.
///
/// Notes:
///  - Implementations are expected to be thread-safe.
///  - This interface is used to manage the collection of frontends dynamically, allowing for runtime updates.
///  - Any changes are not expected to be persisted across application restarts; they are transient and in-memory.
///  - This is not an extensibility point and implementors of this library cannot replace it with a different implementation.
/// </summary>
public interface IFrontendCollection
{
    void AddOrUpdate(BffFrontend frontend);
    void Remove(BffFrontendName frontendName);
    IReadOnlyList<BffFrontend> GetAll();
}
