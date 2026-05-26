// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Text;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

internal static class Base64UrlExtensions
{
    internal static bool TryDecode(string input, out byte[] output)
    {
        try
        {
            output = Base64Url.DecodeFromChars(input);
            return true;
        }
        catch (FormatException)
        {
            output = [];
            return false;
        }
    }
}
