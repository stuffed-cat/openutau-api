using System.Collections.Concurrent;
using System.Threading;

namespace OpenUtau.Api.Services
{
    public static class SessionLockProvider
    {
        private const string GlobalLockKey = "__global__";
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new ConcurrentDictionary<string, SemaphoreSlim>();

        public static SemaphoreSlim GetLock(string? sessionId)
        {
            var key = string.IsNullOrWhiteSpace(sessionId) ? GlobalLockKey : sessionId.Trim();
            return Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        }
    }
}