using System;
using System.Collections.Concurrent;
using System.Threading;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;

namespace OpenUtau.Api.Services
{
    public class UserSession
    {
        public string SessionId { get; set; } = string.Empty;
        public UProject Project { get; set; }
        // We could also store history/undo queue per session if we decoupled it from DocManager
    }

    public class SessionManager
    {
        private static readonly SessionManager _inst = new SessionManager();
        public static SessionManager Inst => _inst;

        private ConcurrentDictionary<string, UserSession> _sessions = new ConcurrentDictionary<string, UserSession>();
        private readonly object _lock = new object();

        // This is a hacky way since the core relies on DocManager.Inst
        public void SwitchSession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            lock (_lock)
            {
                if (!_sessions.ContainsKey(sessionId))
                {
                    _sessions[sessionId] = new UserSession
                    {
                        SessionId = sessionId,
                        Project = new UProject()
                    };
                    // When creating a new project, we also need to build segments
                    _sessions[sessionId].Project.tracks.Add(new UTrack(DocManager.Inst.Project)); // mock init if needed
                }

                // Inject into DocManager
                DocManager.Inst.ExecuteCmd(new LoadProjectNotification(_sessions[sessionId].Project));
            }
        }

        public string CreateSession(UProject initialProject = null)
        {
            var id = Guid.NewGuid().ToString("N");
            _sessions[id] = new UserSession
            {
                SessionId = id,
                Project = initialProject ?? new UProject()
            };
            return id;
        }

        public UProject GetProject(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                return session.Project;
            }
            return null;
        }

        public void DeleteSession(string sessionId)
        {
            _sessions.TryRemove(sessionId, out _);
        }
    }
}
