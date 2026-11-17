// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;
using System.Text;

namespace Duende.IdentityServer.OutsideIn.Harness.Internal;

/// <summary>
/// Issuer-aware snapshot scrubbing for one environment (design doc section 10, D26, D35).
/// <list type="bullet">
/// <item>Whatever the harness creates (URLs, client IDs, secrets, users) registers its value with a placeholder name
/// and, if it belongs to one issuer, that issuer as owner. While a scenario runs for one issuer, values it owns (or
/// that no issuer owns) become <c>{name}</c>; values owned by another issuer become <c>{name@owner}</c>, so data
/// leaking between spaces stays visible.</item>
/// <item>Protocol values (codes, state, nonce, session IDs …) are registered by the harness's response formatting under
/// their field name, and become numbered placeholders such as <c>{Code_1}</c>. Numbering restarts for every issuer's
/// run, so every issuer produces the same snapshot.</item>
/// </list>
/// Values are matched by registration, never by patterns. The registry belongs to the environment, so tests running in
/// parallel never see each other's values.
/// </summary>
internal sealed class SnapshotScrubber
{
    // Short values could match unrelated text. Every generated value the harness sees is far longer than this.
    private const int MinimumProtocolValueLength = 8;

    // Shared with ScenarioResultFormatter, which registers these fields when it finds them in a JSON response body
    // (for example a token or introspection response), the same way TestBrowser registers them when it finds them in
    // a query string or form post.
    internal static readonly HashSet<string> ProtocolFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "code", "state", "nonce", "code_challenge", "session_state", "sid", "jti", "kid", "request_uri",
        "id_token", "access_token", "refresh_token", "id_token_hint", "at_hash", "c_hash", "s_hash",
    };

    // Claims and response fields that hold Unix timestamps, shown relative to the clock's start instead of scrubbed.
    internal static readonly HashSet<string> TimeFields = new(StringComparer.Ordinal) { "auth_time", "iat", "exp", "nbf" };

    private readonly ConcurrentDictionary<string, (string Name, string? Owner)> _values = new(StringComparer.Ordinal);
    private readonly Lock _runLock = new();
    private readonly Dictionary<string, string> _runValues = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _runCounters = new(StringComparer.Ordinal);

    public void Register(string value, string name, string? owner = null) => _values[value] = (name, owner);

    /// <summary>
    /// How <paramref name="value" /> appears in a snapshot during <paramref name="currentIssuer" />'s run, for example
    /// <c>{user:alice}</c> or <c>{user:alice@space-1}</c>, or the value itself when nothing registered it.
    /// </summary>
    public string Describe(string value, string currentIssuer) =>
        _values.TryGetValue(value, out var entry) ? Placeholder(entry, currentIssuer) : value;

    /// <summary>
    /// Every registered placeholder with the values behind it, as seen in <paramref name="currentIssuer" />'s run: the
    /// current issuer's own values first, then values without an owner, then other issuers' values.
    /// </summary>
    public IReadOnlyList<(string Placeholder, IReadOnlyList<string> Values)> DescribeAll(string currentIssuer) =>
        _values
            .GroupBy(pair => Placeholder(pair.Value, currentIssuer))
            .OrderBy(group => group.First().Value.Owner == currentIssuer ? 0 : group.First().Value.Owner is null ? 1 : 2)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (group.Key, (IReadOnlyList<string>)[.. group.Select(pair => pair.Key).Order(StringComparer.Ordinal)]))
            .ToList();

    /// <summary>Starts numbering protocol values from 1 again, for the next issuer's run of the scenario.</summary>
    public void BeginRun()
    {
        lock (_runLock)
        {
            _runValues.Clear();
            _runCounters.Clear();
        }
    }

    /// <summary>Registers a protocol value under its field name, for example <c>code_challenge</c> becomes <c>{CodeChallenge_1}</c>.</summary>
    public void RegisterProtocolValue(string fieldName, string value)
    {
        if (value.Length < MinimumProtocolValueLength)
        {
            return;
        }

        lock (_runLock)
        {
            if (_values.ContainsKey(value) || _runValues.ContainsKey(value))
            {
                return;
            }

            var name = ToPascalCase(fieldName);
            var number = _runCounters[name] = _runCounters.GetValueOrDefault(name) + 1;
            _runValues[value] = $"{name}_{number}";
        }
    }

    public void Scrub(StringBuilder builder, string currentIssuer)
    {
        List<(string Value, string Placeholder)> replacements;
        lock (_runLock)
        {
            replacements = _runValues.Select(pair => (pair.Key, $"{{{pair.Value}}}")).ToList();
        }

        replacements.AddRange(_values.Select(pair => (pair.Key, Placeholder(pair.Value, currentIssuer))));

        // Longest first, so a value is never partly replaced through a shorter value that is its prefix.
        foreach (var (value, placeholder) in replacements.OrderByDescending(replacement => replacement.Value.Length))
        {
            builder.Replace(value, placeholder);
        }
    }

    private static string Placeholder((string Name, string? Owner) entry, string currentIssuer) =>
        entry.Owner is null || entry.Owner == currentIssuer ? $"{{{entry.Name}}}" : $"{{{entry.Name}@{entry.Owner}}}";

    private static string ToPascalCase(string fieldName) =>
        string.Concat(fieldName.Split('_', '-').Where(part => part.Length > 0).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
