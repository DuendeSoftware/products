// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement;

public static class GuidFactory
{
    public static Guid Create(byte version, byte? variant = null, byte? sequence = null)
    {
        var bytes = Guid.AllBitsSet.ToByteArray();
        bytes[8] = (byte)((variant ?? 0x8) << 4);
        bytes[7] = (byte)(version << 4);
        bytes[15] = sequence ?? byte.MaxValue;
        return new Guid(bytes);
    }
}
