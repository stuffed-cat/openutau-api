using Microsoft.AspNetCore.Http;
using OpenUtau.Api.Services;
using System.Threading.Tasks;

namespace OpenUtau.Api.Middlewares
{
    public class SessionMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        private static readonly string[] _bypassPrefixes = new[]
        {
            "/api/renderprogress",   // Render progress status & long-polling sse
            "/api/events",           // Events long-polling sse
            "/api/project/render",   // Independent file rendering (does not use single global project)
            "/api/plugins",          // static plugins querying
            "/api/renderers"         // static renderers querying
        };

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant();

            // Check if the route is exempted from global session locking
            bool bypassLock = false;
            if (path != null)
            {
                foreach (var prefix in _bypassPrefixes)
                {
                    if (path.StartsWith(prefix))
                    {
                        bypassLock = true;
                        break;
                    }
                }
            }

            // Only apply session switch and locking for /api/ routes that are not bypassed
            if (path != null && path.StartsWith("/api/") && !bypassLock)
            {
                // check query string or header for specific sessionId token
                var sessionId = context.Request.Headers["X-Session-Id"].ToString();
                if (string.IsNullOrEmpty(sessionId)) {
                    sessionId = context.Request.Query["sessionId"].ToString();
                }

                // Acquire a session-scoped lock so requests for different sessions can proceed in parallel.
                // Requests without a session id fall back to a shared global lock.
                var gate = SessionLockProvider.GetLock(sessionId);
                await gate.WaitAsync();
                try
                {
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        SessionManager.Inst.SwitchSession(sessionId);
                    }
                    
                    await _next(context);
                }
                finally
                {
                    gate.Release();
                }
            }
            else
            {
                // Non-API routes don't interact with DocManager
                await _next(context);
            }
        }
    }
}
