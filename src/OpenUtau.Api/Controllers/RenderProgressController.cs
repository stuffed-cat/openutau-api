using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OpenUtau.Core;


namespace OpenUtau.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RenderProgressController : ControllerBase
    {
        private readonly RenderProgressMonitor _monitor;

        public RenderProgressController()
        {
            _monitor = RenderProgressMonitor.Instance;
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new {
                progress = _monitor.Progress,
                info = _monitor.Info,
                isRendering = _monitor.Progress > 0 && _monitor.Progress < 100 // Approximation
            });
        }

        [HttpGet("sse")]
        public async Task Sse()
        {
            Response.Headers.Add("Content-Type", "text/event-stream");
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Connection", "keep-alive");

            var tcs = new CancellationTokenSource();
            
            void OnProgressUpdated(object sender, RenderProgressEventArgs e)
            {
                var data = $"{{\"progress\":{e.Progress},\"info\":\"{e.Info}\"}}";
                var bytes = System.Text.Encoding.UTF8.GetBytes($"data: {data}\n\n");
                try {
                    Response.Body.WriteAsync(bytes, 0, bytes.Length).Wait();
                    Response.Body.FlushAsync().Wait();
                } catch {
                    tcs.Cancel();
                }
            }

            _monitor.ProgressUpdated += OnProgressUpdated;

            try
            {
                while (!HttpContext.RequestAborted.IsCancellationRequested && !tcs.IsCancellationRequested)
                {
                    await Task.Delay(1000, HttpContext.RequestAborted);
                }
            }
            catch (TaskCanceledException)
            {
                // Client aborted
            }
            finally
            {
                _monitor.ProgressUpdated -= OnProgressUpdated;
            }
        }
    }

    public class RenderProgressEventArgs : EventArgs
    {
        public double Progress { get; set; }
        public string Info { get; set; }
    }

    public class RenderProgressMonitor : ICmdSubscriber
    {
        private static readonly RenderProgressMonitor _instance = new RenderProgressMonitor();
        public static RenderProgressMonitor Instance => _instance;

        public double Progress { get; private set; }
        public string Info { get; private set; }

        public event EventHandler<RenderProgressEventArgs> ProgressUpdated;

        private RenderProgressMonitor()
        {
            // Subscribe to open utau's command system
            DocManager.Inst.AddSubscriber(this);
        }

        public void OnNext(UCommand cmd, bool isUndo)
        {
            if (cmd is ProgressBarNotification pbn)
            {
                Progress = pbn.Progress;
                Info = pbn.Info;
                ProgressUpdated?.Invoke(this, new RenderProgressEventArgs { Progress = pbn.Progress, Info = pbn.Info });
            }
        }
    }
}
