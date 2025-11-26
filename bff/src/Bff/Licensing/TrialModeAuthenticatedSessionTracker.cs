// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Collections.Concurrent;

namespace Duende.Bff.Licensing;

internal class TrialModeAuthenticatedSessionTracker
{
    private readonly ConcurrentDictionary<string, byte> _authenticatedSessions = new();

    public int UniqueAuthenticatedSessions => _authenticatedSessions.Count;

    public void RecordAuthenticatedSession(string subjectId)
    {
        if (_authenticatedSessions.Count <= LicenseValidator.MaximumAllowedSessionsInTrialMode)
        {
            _authenticatedSessions.TryAdd(subjectId, 0);
        }
    }
}
