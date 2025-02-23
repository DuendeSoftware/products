using System.Collections.Generic;
using System.Linq;

namespace MvcHybrid
{
    public class LogoutSessionManager
    {
        // yes - that needs to be thread-safe, distributed etc (it's a sample)
        List<Session> _sessions = new List<Session>();

        public void Add(string sub, string sid)
        {
            _sessions.Add(new Session { Sub = sub, Sid = sid }); 
        }

        public bool IsLoggedOut(string sub, string sid)
        {
            var matches = _sessions.Any(s => s.IsMatch(sub, sid));
            return matches;
        }

        private class Session
        {
            public string Sub { get; set; }
            public string Sid { get; set; }

            public bool IsMatch(string sub, string sid)
            {
                return (Sid == sid && Sub == sub) ||
                       (Sid == sid && Sub == null) ||
                       (Sid == null && Sub == sub);
            }
        }
    }
}
