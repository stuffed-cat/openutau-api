using System;
using System.Collections.Concurrent;
using System.Threading;
using OpenUtau.Core;
using OpenUtau.Core.Ustx;
using System.IO;
using OpenUtau.Core.Util;

namespace OpenUtau.Api.Services
{
    public class UserSession
    {
        public string SessionId { get; set; } = string.Empty;
        public UProject Project { get; set; }
        // We could also store history/undo queue per session if we decoupled it from DocManager
    }

    public class SessionManager : ICmdSubscriber
    {        private static readonly SessionManager _inst = new SessionManager();
        public static SessionManager Inst => _inst;

        private ConcurrentDictionary<string, UserSession> _sessions = new ConcurrentDictionary<string, UserSession>();
        private readonly object _lock = new object();

        private Timer? _saveTimer;
        private string _sessionsDirectory = string.Empty;
        private ConcurrentDictionary<string, bool> _dirtySessions = new ConcurrentDictionary<string, bool>();
        private string _currentSessionId = string.Empty;

        private SessionManager()
        {
            try {
                _sessionsDirectory = Path.Combine(PathManager.Inst.DataPath, "Sessions");
                Directory.CreateDirectory(_sessionsDirectory);
                LoadAllSessions();
            } catch { }

            DocManager.Inst.AddSubscriber(this);
            _saveTimer = new Timer(OnSaveTimer, null, 5000, 5000);
        }

        private void LoadAllSessions()
        {
            if (!Directory.Exists(_sessionsDirectory)) return;

            foreach (var file in Directory.GetFiles(_sessionsDirectory, "*.ustx"))
            {
                var sessionId = Path.GetFileNameWithoutExtension(file);
                try
                {
                    var proj = OpenUtau.Core.Format.Ustx.Load(file);
                    if (proj != null)
                    {
                        var session = new UserSession
                        {
                            SessionId = sessionId,
                            Project = proj
                        };
                        _sessions[sessionId] = session;
                    }
                }
                catch { }
            }
        }

        public void OnNext(UCommand cmd, bool isUndo)
        {
            if (cmd is UNotification || cmd is LoadProjectNotification) return;
            
            if (!string.IsNullOrEmpty(_currentSessionId))
            {
                _dirtySessions[_currentSessionId] = true;
            }
        }

        private void OnSaveTimer(object? state)
        {
            foreach (var kv in _dirtySessions)
            {
                if (kv.Value)
                {
                    _dirtySessions[kv.Key] = false;
                    SaveSession(kv.Key);
                }
            }
        }

        private void SaveSession(string sessionId)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                try
                {
                    if (string.IsNullOrEmpty(_sessionsDirectory)) return;
                    var path = Path.Combine(_sessionsDirectory, $"{sessionId}.ustx");
                    session.Project.FilePath = path; // Important for relative paths resolving
                    OpenUtau.Core.Format.Ustx.Save(path, session.Project);
                }
                catch { }
            }
        }


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
                        Project = OpenUtau.Core.Format.Ustx.Create()
                    };
                    SaveSession(sessionId); // Initial save
                }

                _currentSessionId = sessionId;

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
                Project = initialProject ?? OpenUtau.Core.Format.Ustx.Create()
            };
            SaveSession(id); // Initial save
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
            if (_sessions.TryRemove(sessionId, out _))
            {
                _dirtySessions.TryRemove(sessionId, out _);
                try
                {
                    if (!string.IsNullOrEmpty(_sessionsDirectory)) {
                        var path = Path.Combine(_sessionsDirectory, $"{sessionId}.ustx");
                        if (File.Exists(path)) File.Delete(path);
                    }
                }
                catch { }
            }
        }
    }
}
