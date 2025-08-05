// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace Web;

public class LogoutSessionManager
{
    // yes, this needs to be thread-safe, distributed etc (it's a sample)
    private readonly List<Session> _sessions = [];

    public void Add(string? sub, string? sid) => _sessions.Add(new Session { Sub = sub, Sid = sid });

    public bool IsLoggedOut(string? sub, string? sid)
    {
        var matches = _sessions.Any(s => s.IsMatch(sub, sid));
        return matches;
    }

    private class Session
    {
        public string? Sub { get; set; }
        public string? Sid { get; set; }

        public bool IsMatch(string? sub, string? sid) => (Sid == sid && Sub == sub) ||
                                                         (Sid == sid && Sub == null) ||
                                                         (Sid == null && Sub == sub);
    }
}
