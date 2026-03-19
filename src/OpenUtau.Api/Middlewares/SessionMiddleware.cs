using Microsoft.AspNetCore.Http;
using OpenUtau.Api.Services;
using System.Threading;
using System.Threading.Tasks;

namespace OpenUtau.Api.Middlewares
{
    public class SessionMiddleware
    {
        private readonly RequestDelegate _next;
        // Global lock to serialize requests and protect OpenUTAU DocManager's single active project state
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

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

                // Acquire the lock: this ensures only ONE API request is processed at any given time,
                // securely protecting the state of the globally shared DocManager.Inst.
                await _semaphore.WaitAsync();
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
                    _semaphore.Release();
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
