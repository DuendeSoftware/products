// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Diagnostics.CodeAnalysis;

namespace Duende.Bff.DynamicFrontends.Internal;

/// <summary>
/// A Trie data structure for matching static URL-style paths to a value.
/// This version does not support parameterized segments.
/// </summary>
/// <typeparam name="TValue">The type of the value to be stored and retrieved.</typeparam>
internal class PathTrie<TValue> where TValue : class
{
    private readonly TrieNode _root = new TrieNode();

    /// <summary>
    /// Represents the result of a match operation.
    /// </summary>
    public readonly struct MatchResult(TValue? value)
    {
        /// <summary>
        /// The value for the matched path. This will be the default value for TValue
        /// (e.g., null for reference types) if no match was found.
        /// </summary>
        public TValue? Value { get; } = value;

        /// <summary>
        /// Indicates whether a value was found.
        /// </summary>
        [MemberNotNullWhen(true, nameof(Value))]
        public bool HasMatch => Value != null;
    }

    private static readonly char[] _pathSeparators = ['/'];

    /// <summary>
    /// Adds a static route path and its associated value to the trie.
    /// </summary>
    /// <param name="path">The route path, e.g., "/about/contact". Segments are separated by '/'.</param>
    /// <param name="value">The value to be stored for this path.</param>
    public void Add(string path, TValue value)
    {
        if (string.IsNullOrEmpty(path))
        {
            _root.Value = value;
            return;
        }

        var currentNode = _root;
        var segments = path.Split(_pathSeparators, StringSplitOptions.RemoveEmptyEntries);

        foreach (var segment in segments)
        {
            if (!currentNode.Children.TryGetValue(segment, out var childNode))
            {
                childNode = new TrieNode();
                currentNode.Children[segment] = childNode;
            }
            currentNode = childNode;
        }

        currentNode.Value = value;
    }

    /// <summary>
    /// Matches the given path against the routes stored in the trie.
    /// </summary>
    /// <param name="path">The path to match, e.g., "/about/contact".</param>
    /// <returns>A MatchResult containing the best-matched value.</returns>
    public MatchResult Match(string path) => Match(path.AsSpan());

    private MatchResult Match(ReadOnlySpan<char> path)
    {
        if (path.Length > 0 && path[0] == '/')
        {
            path = path.Slice(1);
        }

        if (path.Length > 0 && path[path.Length - 1] == '/')
        {
            path = path.Slice(0, path.Length - 1);
        }

        var currentNode = _root;
        var bestMatchValue = _root.Value;

        while (true)
        {
            var slashIndex = path.IndexOf('/');
            var segment = slashIndex == -1 ? path : path.Slice(0, slashIndex);

            if (!currentNode.Children.TryGetValue(segment.ToString(), out var literalChild))
            {
                // No match, break the loop and use the last best match.
                break;
            }

            currentNode = literalChild;
            if (currentNode.Value != null)
            {
                bestMatchValue = currentNode.Value;
            }

            if (slashIndex == -1)
            {
                break;
            }

            path = path.Slice(slashIndex + 1);
        }

        return new MatchResult(bestMatchValue);
    }

    private class TrieNode
    {
        /// <summary>
        /// The value if the path terminates at this node.
        /// </summary>
        public TValue? Value { get; set; }

        /// <summary>
        /// Literal child segments.
        /// </summary>
        public Dictionary<string, TrieNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
