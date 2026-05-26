// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.QrCodes.Internal;

internal static class QrVersionTables
{
    // Flat array indexed by (version - 1) * 4 + (int)eccLevel
    private static readonly QrVersionInfo[] Entries = BuildTable();

    internal static QrVersionInfo Get(int version, QrEccLevel eccLevel)
    {
        if (version < 1 || version > 40)
        {
            throw new ArgumentOutOfRangeException(nameof(version), version, "QR version must be between 1 and 40.");
        }

        return Entries[(version - 1) * 4 + (int)eccLevel];
    }

    private static QrVersionInfo[] BuildTable()
    {
        var table = new QrVersionInfo[160];

        static QrBlockInfo B(int count, int dataCodewords) => new() { Count = count, DataCodewords = dataCodewords };

        static void Set(QrVersionInfo[] t, int version, QrEccLevel ecc, int total, int eccPerBlock,
            QrBlockInfo[] blocks, int[] alignment, int remainder)
        {
            t[(version - 1) * 4 + (int)ecc] = new QrVersionInfo
            {
                Version = version,
                EccLevel = ecc,
                TotalCodewords = total,
                EccCodewordsPerBlock = eccPerBlock,
                BlockGroups = blocks,
                AlignmentPatternPositions = alignment,
                RemainderBits = remainder,
            };
        }

        // Version 1
        Set(table, 1, QrEccLevel.L, 26, 7, [B(1, 19)], [], 0);
        Set(table, 1, QrEccLevel.M, 26, 10, [B(1, 16)], [], 0);
        Set(table, 1, QrEccLevel.Q, 26, 13, [B(1, 13)], [], 0);
        Set(table, 1, QrEccLevel.H, 26, 17, [B(1, 9)], [], 0);

        // Version 2
        Set(table, 2, QrEccLevel.L, 44, 10, [B(1, 34)], [6, 18], 7);
        Set(table, 2, QrEccLevel.M, 44, 16, [B(1, 28)], [6, 18], 7);
        Set(table, 2, QrEccLevel.Q, 44, 22, [B(1, 22)], [6, 18], 7);
        Set(table, 2, QrEccLevel.H, 44, 28, [B(1, 16)], [6, 18], 7);

        // Version 3
        Set(table, 3, QrEccLevel.L, 70, 15, [B(1, 55)], [6, 22], 7);
        Set(table, 3, QrEccLevel.M, 70, 26, [B(1, 44)], [6, 22], 7);
        Set(table, 3, QrEccLevel.Q, 70, 18, [B(2, 17)], [6, 22], 7);
        Set(table, 3, QrEccLevel.H, 70, 22, [B(2, 13)], [6, 22], 7);

        // Version 4
        Set(table, 4, QrEccLevel.L, 100, 20, [B(1, 80)], [6, 26], 7);
        Set(table, 4, QrEccLevel.M, 100, 18, [B(2, 32)], [6, 26], 7);
        Set(table, 4, QrEccLevel.Q, 100, 26, [B(2, 24)], [6, 26], 7);
        Set(table, 4, QrEccLevel.H, 100, 16, [B(4, 9)], [6, 26], 7);

        // Version 5
        Set(table, 5, QrEccLevel.L, 134, 26, [B(1, 108)], [6, 30], 7);
        Set(table, 5, QrEccLevel.M, 134, 24, [B(2, 43)], [6, 30], 7);
        Set(table, 5, QrEccLevel.Q, 134, 18, [B(2, 15), B(2, 16)], [6, 30], 7);
        Set(table, 5, QrEccLevel.H, 134, 22, [B(2, 11), B(2, 12)], [6, 30], 7);

        // Version 6
        Set(table, 6, QrEccLevel.L, 172, 18, [B(2, 68)], [6, 34], 7);
        Set(table, 6, QrEccLevel.M, 172, 16, [B(4, 27)], [6, 34], 7);
        Set(table, 6, QrEccLevel.Q, 172, 24, [B(4, 19)], [6, 34], 7);
        Set(table, 6, QrEccLevel.H, 172, 28, [B(4, 15)], [6, 34], 7);

        // Version 7
        Set(table, 7, QrEccLevel.L, 196, 20, [B(2, 78)], [6, 22, 38], 0);
        Set(table, 7, QrEccLevel.M, 196, 18, [B(4, 31)], [6, 22, 38], 0);
        Set(table, 7, QrEccLevel.Q, 196, 18, [B(2, 14), B(4, 15)], [6, 22, 38], 0);
        Set(table, 7, QrEccLevel.H, 196, 26, [B(4, 13), B(1, 14)], [6, 22, 38], 0);

        // Version 8
        Set(table, 8, QrEccLevel.L, 242, 24, [B(2, 97)], [6, 24, 42], 0);
        Set(table, 8, QrEccLevel.M, 242, 22, [B(2, 38), B(2, 39)], [6, 24, 42], 0);
        Set(table, 8, QrEccLevel.Q, 242, 22, [B(4, 18), B(2, 19)], [6, 24, 42], 0);
        Set(table, 8, QrEccLevel.H, 242, 26, [B(4, 14), B(2, 15)], [6, 24, 42], 0);

        // Version 9
        Set(table, 9, QrEccLevel.L, 292, 30, [B(2, 116)], [6, 26, 46], 0);
        Set(table, 9, QrEccLevel.M, 292, 22, [B(3, 36), B(2, 37)], [6, 26, 46], 0);
        Set(table, 9, QrEccLevel.Q, 292, 20, [B(4, 16), B(4, 17)], [6, 26, 46], 0);
        Set(table, 9, QrEccLevel.H, 292, 24, [B(4, 12), B(4, 13)], [6, 26, 46], 0);

        // Version 10
        Set(table, 10, QrEccLevel.L, 346, 18, [B(2, 68), B(2, 69)], [6, 28, 50], 0);
        Set(table, 10, QrEccLevel.M, 346, 26, [B(4, 43), B(1, 44)], [6, 28, 50], 0);
        Set(table, 10, QrEccLevel.Q, 346, 24, [B(6, 19), B(2, 20)], [6, 28, 50], 0);
        Set(table, 10, QrEccLevel.H, 346, 28, [B(6, 15), B(2, 16)], [6, 28, 50], 0);

        // Version 11
        Set(table, 11, QrEccLevel.L, 404, 20, [B(4, 81)], [6, 30, 54], 0);
        Set(table, 11, QrEccLevel.M, 404, 30, [B(1, 50), B(4, 51)], [6, 30, 54], 0);
        Set(table, 11, QrEccLevel.Q, 404, 28, [B(4, 22), B(4, 23)], [6, 30, 54], 0);
        Set(table, 11, QrEccLevel.H, 404, 24, [B(3, 12), B(8, 13)], [6, 30, 54], 0);

        // Version 12
        Set(table, 12, QrEccLevel.L, 466, 24, [B(2, 92), B(2, 93)], [6, 32, 58], 0);
        Set(table, 12, QrEccLevel.M, 466, 22, [B(6, 36), B(2, 37)], [6, 32, 58], 0);
        Set(table, 12, QrEccLevel.Q, 466, 26, [B(4, 20), B(6, 21)], [6, 32, 58], 0);
        Set(table, 12, QrEccLevel.H, 466, 28, [B(7, 14), B(4, 15)], [6, 32, 58], 0);

        // Version 13
        Set(table, 13, QrEccLevel.L, 532, 26, [B(4, 107)], [6, 34, 62], 0);
        Set(table, 13, QrEccLevel.M, 532, 22, [B(8, 37), B(1, 38)], [6, 34, 62], 0);
        Set(table, 13, QrEccLevel.Q, 532, 24, [B(8, 20), B(4, 21)], [6, 34, 62], 0);
        Set(table, 13, QrEccLevel.H, 532, 22, [B(12, 11), B(4, 12)], [6, 34, 62], 0);

        // Version 14
        Set(table, 14, QrEccLevel.L, 581, 30, [B(3, 115), B(1, 116)], [6, 26, 46, 66], 3);
        Set(table, 14, QrEccLevel.M, 581, 24, [B(4, 40), B(5, 41)], [6, 26, 46, 66], 3);
        Set(table, 14, QrEccLevel.Q, 581, 20, [B(11, 16), B(5, 17)], [6, 26, 46, 66], 3);
        Set(table, 14, QrEccLevel.H, 581, 24, [B(11, 12), B(5, 13)], [6, 26, 46, 66], 3);

        // Version 15
        Set(table, 15, QrEccLevel.L, 655, 22, [B(5, 87), B(1, 88)], [6, 26, 48, 70], 3);
        Set(table, 15, QrEccLevel.M, 655, 24, [B(5, 41), B(5, 42)], [6, 26, 48, 70], 3);
        Set(table, 15, QrEccLevel.Q, 655, 30, [B(5, 24), B(7, 25)], [6, 26, 48, 70], 3);
        Set(table, 15, QrEccLevel.H, 655, 24, [B(11, 12), B(7, 13)], [6, 26, 48, 70], 3);

        // Version 16
        Set(table, 16, QrEccLevel.L, 733, 24, [B(5, 98), B(1, 99)], [6, 26, 50, 74], 3);
        Set(table, 16, QrEccLevel.M, 733, 28, [B(7, 45), B(3, 46)], [6, 26, 50, 74], 3);
        Set(table, 16, QrEccLevel.Q, 733, 24, [B(15, 19), B(2, 20)], [6, 26, 50, 74], 3);
        Set(table, 16, QrEccLevel.H, 733, 30, [B(3, 15), B(13, 16)], [6, 26, 50, 74], 3);

        // Version 17
        Set(table, 17, QrEccLevel.L, 815, 28, [B(1, 107), B(5, 108)], [6, 30, 54, 78], 3);
        Set(table, 17, QrEccLevel.M, 815, 28, [B(10, 46), B(1, 47)], [6, 30, 54, 78], 3);
        Set(table, 17, QrEccLevel.Q, 815, 28, [B(1, 22), B(15, 23)], [6, 30, 54, 78], 3);
        Set(table, 17, QrEccLevel.H, 815, 28, [B(2, 14), B(17, 15)], [6, 30, 54, 78], 3);

        // Version 18
        Set(table, 18, QrEccLevel.L, 901, 30, [B(5, 120), B(1, 121)], [6, 30, 56, 82], 3);
        Set(table, 18, QrEccLevel.M, 901, 26, [B(9, 43), B(4, 44)], [6, 30, 56, 82], 3);
        Set(table, 18, QrEccLevel.Q, 901, 28, [B(17, 22), B(1, 23)], [6, 30, 56, 82], 3);
        Set(table, 18, QrEccLevel.H, 901, 28, [B(2, 14), B(19, 15)], [6, 30, 56, 82], 3);

        // Version 19
        Set(table, 19, QrEccLevel.L, 991, 28, [B(3, 113), B(4, 114)], [6, 30, 58, 86], 3);
        Set(table, 19, QrEccLevel.M, 991, 26, [B(3, 44), B(11, 45)], [6, 30, 58, 86], 3);
        Set(table, 19, QrEccLevel.Q, 991, 26, [B(17, 21), B(4, 22)], [6, 30, 58, 86], 3);
        Set(table, 19, QrEccLevel.H, 991, 26, [B(9, 13), B(16, 14)], [6, 30, 58, 86], 3);

        // Version 20
        Set(table, 20, QrEccLevel.L, 1085, 28, [B(3, 107), B(5, 108)], [6, 34, 62, 90], 3);
        Set(table, 20, QrEccLevel.M, 1085, 26, [B(3, 41), B(13, 42)], [6, 34, 62, 90], 3);
        Set(table, 20, QrEccLevel.Q, 1085, 30, [B(15, 24), B(5, 25)], [6, 34, 62, 90], 3);
        Set(table, 20, QrEccLevel.H, 1085, 28, [B(15, 15), B(10, 16)], [6, 34, 62, 90], 3);

        // Version 21
        Set(table, 21, QrEccLevel.L, 1156, 28, [B(4, 116), B(4, 117)], [6, 28, 50, 72, 94], 4);
        Set(table, 21, QrEccLevel.M, 1156, 26, [B(17, 42)], [6, 28, 50, 72, 94], 4);
        Set(table, 21, QrEccLevel.Q, 1156, 28, [B(17, 22), B(6, 23)], [6, 28, 50, 72, 94], 4);
        Set(table, 21, QrEccLevel.H, 1156, 30, [B(19, 16), B(6, 17)], [6, 28, 50, 72, 94], 4);

        // Version 22
        Set(table, 22, QrEccLevel.L, 1258, 28, [B(2, 111), B(7, 112)], [6, 26, 50, 74, 98], 4);
        Set(table, 22, QrEccLevel.M, 1258, 28, [B(17, 46)], [6, 26, 50, 74, 98], 4);
        Set(table, 22, QrEccLevel.Q, 1258, 30, [B(7, 24), B(16, 25)], [6, 26, 50, 74, 98], 4);
        Set(table, 22, QrEccLevel.H, 1258, 24, [B(34, 13)], [6, 26, 50, 74, 98], 4);

        // Version 23
        Set(table, 23, QrEccLevel.L, 1364, 30, [B(4, 121), B(5, 122)], [6, 30, 54, 78, 102], 4);
        Set(table, 23, QrEccLevel.M, 1364, 28, [B(4, 47), B(14, 48)], [6, 30, 54, 78, 102], 4);
        Set(table, 23, QrEccLevel.Q, 1364, 30, [B(11, 24), B(14, 25)], [6, 30, 54, 78, 102], 4);
        Set(table, 23, QrEccLevel.H, 1364, 30, [B(16, 15), B(14, 16)], [6, 30, 54, 78, 102], 4);

        // Version 24
        Set(table, 24, QrEccLevel.L, 1474, 30, [B(6, 117), B(4, 118)], [6, 28, 54, 80, 106], 4);
        Set(table, 24, QrEccLevel.M, 1474, 28, [B(6, 45), B(14, 46)], [6, 28, 54, 80, 106], 4);
        Set(table, 24, QrEccLevel.Q, 1474, 30, [B(11, 24), B(16, 25)], [6, 28, 54, 80, 106], 4);
        Set(table, 24, QrEccLevel.H, 1474, 30, [B(30, 16), B(2, 17)], [6, 28, 54, 80, 106], 4);

        // Version 25
        Set(table, 25, QrEccLevel.L, 1588, 26, [B(8, 106), B(4, 107)], [6, 32, 58, 84, 110], 4);
        Set(table, 25, QrEccLevel.M, 1588, 28, [B(8, 47), B(13, 48)], [6, 32, 58, 84, 110], 4);
        Set(table, 25, QrEccLevel.Q, 1588, 30, [B(7, 24), B(22, 25)], [6, 32, 58, 84, 110], 4);
        Set(table, 25, QrEccLevel.H, 1588, 30, [B(22, 15), B(13, 16)], [6, 32, 58, 84, 110], 4);

        // Version 26
        Set(table, 26, QrEccLevel.L, 1706, 28, [B(10, 114), B(2, 115)], [6, 30, 58, 86, 114], 4);
        Set(table, 26, QrEccLevel.M, 1706, 28, [B(19, 46), B(4, 47)], [6, 30, 58, 86, 114], 4);
        Set(table, 26, QrEccLevel.Q, 1706, 28, [B(28, 22), B(6, 23)], [6, 30, 58, 86, 114], 4);
        Set(table, 26, QrEccLevel.H, 1706, 30, [B(33, 16), B(4, 17)], [6, 30, 58, 86, 114], 4);

        // Version 27
        Set(table, 27, QrEccLevel.L, 1828, 30, [B(8, 122), B(4, 123)], [6, 34, 62, 90, 118], 4);
        Set(table, 27, QrEccLevel.M, 1828, 28, [B(22, 45), B(3, 46)], [6, 34, 62, 90, 118], 4);
        Set(table, 27, QrEccLevel.Q, 1828, 30, [B(8, 23), B(26, 24)], [6, 34, 62, 90, 118], 4);
        Set(table, 27, QrEccLevel.H, 1828, 30, [B(12, 15), B(28, 16)], [6, 34, 62, 90, 118], 4);

        // Version 28
        Set(table, 28, QrEccLevel.L, 1921, 30, [B(3, 117), B(10, 118)], [6, 26, 50, 74, 98, 122], 3);
        Set(table, 28, QrEccLevel.M, 1921, 28, [B(3, 45), B(23, 46)], [6, 26, 50, 74, 98, 122], 3);
        Set(table, 28, QrEccLevel.Q, 1921, 30, [B(4, 24), B(31, 25)], [6, 26, 50, 74, 98, 122], 3);
        Set(table, 28, QrEccLevel.H, 1921, 30, [B(11, 15), B(31, 16)], [6, 26, 50, 74, 98, 122], 3);

        // Version 29
        Set(table, 29, QrEccLevel.L, 2051, 30, [B(7, 116), B(7, 117)], [6, 30, 54, 78, 102, 126], 3);
        Set(table, 29, QrEccLevel.M, 2051, 28, [B(21, 45), B(7, 46)], [6, 30, 54, 78, 102, 126], 3);
        Set(table, 29, QrEccLevel.Q, 2051, 30, [B(1, 23), B(37, 24)], [6, 30, 54, 78, 102, 126], 3);
        Set(table, 29, QrEccLevel.H, 2051, 30, [B(19, 15), B(26, 16)], [6, 30, 54, 78, 102, 126], 3);

        // Version 30
        Set(table, 30, QrEccLevel.L, 2185, 30, [B(5, 115), B(10, 116)], [6, 26, 52, 78, 104, 130], 3);
        Set(table, 30, QrEccLevel.M, 2185, 28, [B(19, 47), B(10, 48)], [6, 26, 52, 78, 104, 130], 3);
        Set(table, 30, QrEccLevel.Q, 2185, 30, [B(15, 24), B(25, 25)], [6, 26, 52, 78, 104, 130], 3);
        Set(table, 30, QrEccLevel.H, 2185, 30, [B(23, 15), B(25, 16)], [6, 26, 52, 78, 104, 130], 3);

        // Version 31
        Set(table, 31, QrEccLevel.L, 2323, 30, [B(13, 115), B(3, 116)], [6, 30, 56, 82, 108, 134], 3);
        Set(table, 31, QrEccLevel.M, 2323, 28, [B(2, 46), B(29, 47)], [6, 30, 56, 82, 108, 134], 3);
        Set(table, 31, QrEccLevel.Q, 2323, 30, [B(42, 24), B(1, 25)], [6, 30, 56, 82, 108, 134], 3);
        Set(table, 31, QrEccLevel.H, 2323, 30, [B(23, 15), B(28, 16)], [6, 30, 56, 82, 108, 134], 3);

        // Version 32
        Set(table, 32, QrEccLevel.L, 2465, 30, [B(17, 115)], [6, 34, 60, 86, 112, 138], 3);
        Set(table, 32, QrEccLevel.M, 2465, 28, [B(10, 46), B(23, 47)], [6, 34, 60, 86, 112, 138], 3);
        Set(table, 32, QrEccLevel.Q, 2465, 30, [B(10, 24), B(35, 25)], [6, 34, 60, 86, 112, 138], 3);
        Set(table, 32, QrEccLevel.H, 2465, 30, [B(19, 15), B(35, 16)], [6, 34, 60, 86, 112, 138], 3);

        // Version 33
        Set(table, 33, QrEccLevel.L, 2611, 30, [B(17, 115), B(1, 116)], [6, 30, 58, 86, 114, 142], 3);
        Set(table, 33, QrEccLevel.M, 2611, 28, [B(14, 46), B(21, 47)], [6, 30, 58, 86, 114, 142], 3);
        Set(table, 33, QrEccLevel.Q, 2611, 30, [B(29, 24), B(19, 25)], [6, 30, 58, 86, 114, 142], 3);
        Set(table, 33, QrEccLevel.H, 2611, 30, [B(11, 15), B(46, 16)], [6, 30, 58, 86, 114, 142], 3);

        // Version 34
        Set(table, 34, QrEccLevel.L, 2761, 30, [B(13, 115), B(6, 116)], [6, 34, 62, 90, 118, 146], 3);
        Set(table, 34, QrEccLevel.M, 2761, 28, [B(14, 46), B(23, 47)], [6, 34, 62, 90, 118, 146], 3);
        Set(table, 34, QrEccLevel.Q, 2761, 30, [B(44, 24), B(7, 25)], [6, 34, 62, 90, 118, 146], 3);
        Set(table, 34, QrEccLevel.H, 2761, 30, [B(59, 16), B(1, 17)], [6, 34, 62, 90, 118, 146], 3);

        // Version 35
        Set(table, 35, QrEccLevel.L, 2876, 30, [B(12, 121), B(7, 122)], [6, 30, 54, 78, 102, 126, 150], 0);
        Set(table, 35, QrEccLevel.M, 2876, 28, [B(12, 47), B(26, 48)], [6, 30, 54, 78, 102, 126, 150], 0);
        Set(table, 35, QrEccLevel.Q, 2876, 30, [B(39, 24), B(14, 25)], [6, 30, 54, 78, 102, 126, 150], 0);
        Set(table, 35, QrEccLevel.H, 2876, 30, [B(22, 15), B(41, 16)], [6, 30, 54, 78, 102, 126, 150], 0);

        // Version 36
        Set(table, 36, QrEccLevel.L, 3034, 30, [B(6, 121), B(14, 122)], [6, 24, 50, 76, 102, 128, 154], 0);
        Set(table, 36, QrEccLevel.M, 3034, 28, [B(6, 47), B(34, 48)], [6, 24, 50, 76, 102, 128, 154], 0);
        Set(table, 36, QrEccLevel.Q, 3034, 30, [B(46, 24), B(10, 25)], [6, 24, 50, 76, 102, 128, 154], 0);
        Set(table, 36, QrEccLevel.H, 3034, 30, [B(2, 15), B(64, 16)], [6, 24, 50, 76, 102, 128, 154], 0);

        // Version 37
        Set(table, 37, QrEccLevel.L, 3196, 30, [B(17, 122), B(4, 123)], [6, 28, 54, 80, 106, 132, 158], 0);
        Set(table, 37, QrEccLevel.M, 3196, 28, [B(29, 46), B(14, 47)], [6, 28, 54, 80, 106, 132, 158], 0);
        Set(table, 37, QrEccLevel.Q, 3196, 30, [B(49, 24), B(10, 25)], [6, 28, 54, 80, 106, 132, 158], 0);
        Set(table, 37, QrEccLevel.H, 3196, 30, [B(24, 15), B(46, 16)], [6, 28, 54, 80, 106, 132, 158], 0);

        // Version 38
        Set(table, 38, QrEccLevel.L, 3362, 30, [B(4, 122), B(18, 123)], [6, 32, 58, 84, 110, 136, 162], 0);
        Set(table, 38, QrEccLevel.M, 3362, 28, [B(13, 46), B(32, 47)], [6, 32, 58, 84, 110, 136, 162], 0);
        Set(table, 38, QrEccLevel.Q, 3362, 30, [B(48, 24), B(14, 25)], [6, 32, 58, 84, 110, 136, 162], 0);
        Set(table, 38, QrEccLevel.H, 3362, 30, [B(42, 15), B(32, 16)], [6, 32, 58, 84, 110, 136, 162], 0);

        // Version 39
        Set(table, 39, QrEccLevel.L, 3532, 30, [B(20, 117), B(4, 118)], [6, 26, 54, 82, 110, 138, 166], 0);
        Set(table, 39, QrEccLevel.M, 3532, 28, [B(40, 47), B(7, 48)], [6, 26, 54, 82, 110, 138, 166], 0);
        Set(table, 39, QrEccLevel.Q, 3532, 30, [B(43, 24), B(22, 25)], [6, 26, 54, 82, 110, 138, 166], 0);
        Set(table, 39, QrEccLevel.H, 3532, 30, [B(10, 15), B(67, 16)], [6, 26, 54, 82, 110, 138, 166], 0);

        // Version 40
        Set(table, 40, QrEccLevel.L, 3706, 30, [B(19, 118), B(6, 119)], [6, 30, 58, 86, 114, 142, 170], 0);
        Set(table, 40, QrEccLevel.M, 3706, 28, [B(18, 47), B(31, 48)], [6, 30, 58, 86, 114, 142, 170], 0);
        Set(table, 40, QrEccLevel.Q, 3706, 30, [B(34, 24), B(34, 25)], [6, 30, 58, 86, 114, 142, 170], 0);
        Set(table, 40, QrEccLevel.H, 3706, 30, [B(20, 15), B(61, 16)], [6, 30, 58, 86, 114, 142, 170], 0);

        return table;
    }
}
