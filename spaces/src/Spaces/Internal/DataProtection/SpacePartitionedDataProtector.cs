// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Duende.Spaces.Internal.Telemetry;
using Microsoft.AspNetCore.DataProtection;

namespace Duende.Spaces.Internal.DataProtection;

internal sealed class SpacePartitionedDataProtector : IDataProtector, IPersistedDataProtector, IUnpartitionedDataProtectionSource
{
    private readonly IDataProtectionProvider _root;
    private readonly ISpaceContextAccessor _accessor;
    private readonly string[] _purposes;
    private readonly SpacePartitioningContext _context;
    private readonly Lazy<IDataProtector> _unpartitioned;

    public SpacePartitionedDataProtector(IDataProtectionProvider root, ISpaceContextAccessor accessor, string[] purposes, SpacePartitioningContext context)
    {
        _root = root;
        _accessor = accessor;
        _purposes = purposes;
        _context = context;
        _unpartitioned = new Lazy<IDataProtector>(() => Chain(_root));
    }

    public IDataProtector CreateProtector(string purpose) =>
        new SpacePartitionedDataProtector(_root, _accessor, [.. _purposes, purpose], _context);

    public IDataProtectionProvider GetUnpartitioned() => _unpartitioned.Value;

    public byte[] Protect(byte[] plaintext) => Current(out _).Protect(plaintext);

    public byte[] Unprotect(byte[] protectedData)
    {
        var protector = Current(out var spaceId);
        try
        {
            return protector.Unprotect(protectedData);
        }
        catch (CryptographicException ex) when (spaceId == SpaceId.Default)
        {
            return UnprotectWithUnpartitioned(protectedData, ExceptionDispatchInfo.Capture(ex));
        }
    }

    public byte[] DangerousUnprotect(byte[] protectedData, bool ignoreRevocationErrors, out bool requiresMigration, out bool wasRevoked)
    {
        var protector = Current(out var spaceId);
        if (protector is not IPersistedDataProtector persisted)
        {
            throw new NotSupportedException(
                "The underlying data protector does not support IPersistedDataProtector.DangerousUnprotect.");
        }

        try
        {
            return persisted.DangerousUnprotect(protectedData, ignoreRevocationErrors, out requiresMigration, out wasRevoked);
        }
        catch (CryptographicException ex) when (spaceId == SpaceId.Default)
        {
            return DangerousUnprotectWithUnpartitioned(protectedData, ignoreRevocationErrors, ExceptionDispatchInfo.Capture(ex), out requiresMigration, out wasRevoked);
        }
    }

    private byte[] UnprotectWithUnpartitioned(byte[] protectedData, ExceptionDispatchInfo original)
    {
        byte[] result;
        try
        {
            result = _unpartitioned.Value.Unprotect(protectedData);
        }
        catch (Exception)
        {
            original.Throw();
            throw;
        }

        _context.Metrics.RecordDefaultSpaceFallback(SpacesTelemetryConstants.TagValues.Unprotect);
        _context.Logger.DefaultSpaceFallback();
        return result;
    }

    private byte[] DangerousUnprotectWithUnpartitioned(byte[] protectedData, bool ignoreRevocationErrors, ExceptionDispatchInfo original, out bool requiresMigration, out bool wasRevoked)
    {
        if (_unpartitioned.Value is not IPersistedDataProtector persisted)
        {
            throw new NotSupportedException(
                "The underlying data protector does not support IPersistedDataProtector.DangerousUnprotect.");
        }

        byte[] result;
        try
        {
            result = persisted.DangerousUnprotect(protectedData, ignoreRevocationErrors, out _, out wasRevoked);
        }
        catch (Exception)
        {
            original.Throw();
            throw;
        }

        requiresMigration = true;
        _context.Metrics.RecordDefaultSpaceFallback(SpacesTelemetryConstants.TagValues.DangerousUnprotect);
        _context.Logger.DefaultSpaceFallback();
        return result;
    }

    private IDataProtector Current(out SpaceId spaceId)
    {
        // Fail closed: using the root or Default keys without a resolved space would silently accept
        // values across spaces, which is exactly what partitioning prevents.
        if (!_accessor.IsSpaceIdConfigured())
        {
            throw new InvalidOperationException(
                "Data protection was used with no resolved space while SpacesOptions.PartitionDataProtection is enabled. " +
                "Call UseSpaceResolution() before middleware that uses data protection, and run background work inside ISpaceContextAccessor.SetSpace(...). " +
                "Data shared across spaces must use GetUnpartitioned().");
        }

        var id = _accessor.GetSpaceId();
        spaceId = id;
        return Chain(_root.CreateProtector("space", id.Value.ToString("D")));
    }

    private IDataProtector Chain(IDataProtectionProvider provider)
    {
        var protector = provider.CreateProtector(_purposes[0]);
        for (var i = 1; i < _purposes.Length; i++)
        {
            protector = protector.CreateProtector(_purposes[i]);
        }

        return protector;
    }
}
