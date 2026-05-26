// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

internal sealed class QrBlockInfo
{
    internal int Count { get; init; }
    internal int DataCodewords { get; init; }
}

internal sealed class QrVersionInfo
{
    internal int Version { get; init; }
    internal QrEccLevel EccLevel { get; init; }
    internal int TotalCodewords { get; init; }
    internal int EccCodewordsPerBlock { get; init; }

#pragma warning disable CA1819
    internal QrBlockInfo[] BlockGroups { get; init; } = [];
    internal int[] AlignmentPatternPositions { get; init; } = [];
#pragma warning restore CA1819

    internal int RemainderBits { get; init; }

    internal int DataCodewords => BlockGroups.Sum(g => g.Count * g.DataCodewords);
}
