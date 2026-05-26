// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Duende.UserManagement.Authentication.Internal;

internal static class StringExtensions
{
    internal static IEnumerable<string> ToGroups(this string text)
    {
        if (text.Length <= 4)
        {
            return [text];
        }

        var groupSize = GetGroupSize(text.Length);

        return Enumerable.Range(0, (text.Length + groupSize - 1) / groupSize)
            .Select(i => text.Substring(i * groupSize, Math.Min(groupSize, text.Length - i * groupSize)));

        static int GetGroupSize(int n)
        {
            var nextMultipleOf5 = NextMultiple(n, 5);
            var nextMultipleOf4 = NextMultiple(n, 4);
            var nextMultipleOf3 = NextMultiple(n, 3);

            var minNextMultiple = Math.Min(nextMultipleOf5, Math.Min(nextMultipleOf4, nextMultipleOf3));
            return minNextMultiple == nextMultipleOf5 ? 5 : minNextMultiple == nextMultipleOf4 ? 4 : 3;

            static int NextMultiple(int n, int d)
                => ((n + d - 1) / d) * d;
        }
    }
}
