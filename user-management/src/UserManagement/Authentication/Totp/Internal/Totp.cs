// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Duende.UserManagement.Authentication.Totp.Internal;

// https://datatracker.ietf.org/doc/html/rfc6238
internal static class Totp
{
    internal static bool Validate(
        IReadOnlyCollection<byte> key,
        byte totpLength,
        ulong unixTimeSeconds,
        string totp,
        ulong? lastSuccessfulTimeStep,
        [NotNullWhen(true)] out ulong? successfulTimeStep)
    {
        successfulTimeStep = null;
        const int maxClockDifferenceSeconds = 30;
        const int timeStepSeconds = 30;

        // time consistency
        var validated = false;
        var minTimeStep = ((long)unixTimeSeconds - maxClockDifferenceSeconds) / timeStepSeconds;
        var maxTimeStep = ((long)unixTimeSeconds + maxClockDifferenceSeconds) / timeStepSeconds;
        for (var signedTimeStep = minTimeStep; signedTimeStep <= maxTimeStep; ++signedTimeStep)
        {
            var unsignedTimeStep = signedTimeStep < 0L ? 0UL : (ulong)signedTimeStep;
            var timeStepBytes = BitConverter.GetBytes(
                BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(unsignedTimeStep) : unsignedTimeStep);

#pragma warning disable CA5350 // HMAC-SHA1 is required by RFC 6238 (TOTP) and RFC 4226 (HOTP) as the default algorithm
            using var hmac = new HMACSHA1([.. key]);
#pragma warning restore CA5350

            var hash = hmac.ComputeHash(timeStepBytes);

            var offset = hash[^1] & 0xf;
            var binary =
                ((hash[offset] & 0x7f) << 24) |
                ((hash[offset + 1] & 0xff) << 16) |
                ((hash[offset + 2] & 0xff) << 8) |
                (hash[offset + 3] & 0xff);

            var numericTotp = binary % (int)Math.Pow(10, totpLength);
            var textTotp = numericTotp.ToString("D", CultureInfo.InvariantCulture).PadLeft(totpLength, '0');

            var totpIsValid = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(totp), Encoding.ASCII.GetBytes(textTotp));
            var timeStepPreviouslyUsed = unsignedTimeStep <= lastSuccessfulTimeStep;

            if (totpIsValid && !timeStepPreviouslyUsed)
            {
                successfulTimeStep = unsignedTimeStep;
                validated = true;
            }
        }

        return validated;
    }
}
