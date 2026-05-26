// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.Formats.Cbor;

namespace Duende.UserManagement.Authentication.Passkeys.Internal;

/// <summary>
/// Parsed attestation object from CBOR.
/// </summary>
internal sealed record AttestationObject(
    string Format,
    byte[] AuthData,
    IReadOnlyDictionary<string, object?> AttStmt)
{
    private static class CborKeys
    {
        public const string Format = "fmt";
        public const string AuthData = "authData";
        public const string AttestationStatement = "attStmt";
    }

    internal static bool TryParse(ReadOnlySpan<byte> cbor, [NotNullWhen(true)] out AttestationObject? result)
    {
        result = null;

        try
        {
            var reader = new CborReader(cbor.ToArray());

            if (reader.PeekState() != CborReaderState.StartMap)
            {
                return false;
            }

            string? format = null;
            byte[]? authData = null;
            Dictionary<string, object?> attStmt = new();

            var mapLength = reader.ReadStartMap();

            for (var i = 0; i < mapLength; i++)
            {
                var key = reader.ReadTextString();

                switch (key)
                {
                    case CborKeys.Format:
                        format = reader.ReadTextString();
                        break;
                    case CborKeys.AuthData:
                        authData = reader.ReadByteString();
                        break;
                    case CborKeys.AttestationStatement:
                        attStmt = ReadAttStmt(reader);
                        break;
                    default:
                        reader.SkipValue();
                        break;
                }
            }

            reader.ReadEndMap();

            if (format is null || authData is null)
            {
                return false;
            }

            result = new AttestationObject(format, authData, attStmt);
            return true;
        }
        catch (CborContentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static Dictionary<string, object?> ReadAttStmt(CborReader reader)
    {
        var result = new Dictionary<string, object?>();

        if (reader.PeekState() != CborReaderState.StartMap)
        {
            reader.SkipValue();
            return result;
        }

        var mapLength = reader.ReadStartMap();

        for (var i = 0; i < mapLength; i++)
        {
            var key = reader.ReadTextString();
            var value = ReadCborValue(reader);
            result[key] = value;
        }

        reader.ReadEndMap();
        return result;
    }

    private static object? ReadCborValue(CborReader reader) =>
        reader.PeekState() switch
        {
            CborReaderState.TextString => reader.ReadTextString(),
            CborReaderState.ByteString => reader.ReadByteString(),
            CborReaderState.UnsignedInteger => reader.ReadInt64(),
            CborReaderState.NegativeInteger => reader.ReadInt64(),
            CborReaderState.Boolean => reader.ReadBoolean(),
            CborReaderState.Null => ReadNull(reader),
            CborReaderState.StartArray => ReadArray(reader),
            CborReaderState.StartMap => ReadMap(reader),
            _ => SkipAndReturnNull(reader)
        };

    private static object? ReadNull(CborReader reader)
    {
        reader.ReadNull();
        return null!;
    }

    private static object? SkipAndReturnNull(CborReader reader)
    {
        reader.SkipValue();
        return null;
    }

    private static object?[] ReadArray(CborReader reader)
    {
        var length = reader.ReadStartArray();
        var items = new object?[length ?? 0];

        for (var i = 0; i < items.Length; i++)
        {
            items[i] = ReadCborValue(reader);
        }

        reader.ReadEndArray();
        return items;
    }

    private static Dictionary<string, object?> ReadMap(CborReader reader)
    {
        var result = new Dictionary<string, object?>();
        var mapLength = reader.ReadStartMap();

        for (var i = 0; i < mapLength; i++)
        {
            var key = reader.ReadTextString();
            var value = ReadCborValue(reader);
            result[key] = value;
        }

        reader.ReadEndMap();
        return result;
    }
}
