// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.Storage.Internal.Builder;

internal sealed class DataStorageTypeRegistry
{
    private readonly Dictionary<DataStorageObjectVersion, Type> _dsoRegistrations;

    public DataStorageTypeRegistry(IEnumerable<DsoRegistration> dsoRegistrations)
        => _dsoRegistrations = dsoRegistrations.ToDictionary(r => r.DsoVersion, r => r.DsoType);

    /// <summary>
    /// Gets the registered type for the specified DSO version.
    /// </summary>
    /// <param name="dsoVersion">The DSO version to look up.</param>
    /// <returns>The registered type.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the DSO type is not registered.</exception>
    public Type Get(DataStorageObjectVersion dsoVersion) =>
        !_dsoRegistrations.TryGetValue(dsoVersion, out var registeredType)
            ? throw new InvalidOperationException($"DsoType {dsoVersion.EntityType.Name} with version {dsoVersion.SchemaVersion} is not registered.")
            : registeredType;

    /// <summary>
    /// Tries to get the registered type for the specified DSO version.
    /// </summary>
    /// <param name="dsoVersion">The DSO version to look up.</param>
    /// <param name="registeredType">The registered type, if found.</param>
    /// <returns><c>true</c> if the type was found; otherwise <c>false</c>.</returns>
    public bool TryGet(DataStorageObjectVersion dsoVersion, out Type registeredType) =>
        _dsoRegistrations.TryGetValue(dsoVersion, out registeredType!);
}
