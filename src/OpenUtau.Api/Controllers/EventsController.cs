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
    public class EventsController : ControllerBase
    {
        private readonly EventsMonitor _monitor;

        public EventsController()
        {
            _monitor = EventsMonitor.Instance;
        }

        [HttpGet("sse")]
        public async Task Sse()
        {
            Response.Headers.Add("Content-Type", "text/event-stream");
            Response.Headers.Add("Cache-Control", "no-cache");
            Response.Headers.Add("Connection", "keep-alive");

            var tcs = new CancellationTokenSource();
            
            void OnEventReceived(object sender, UtauEventArgs e)
            {
                var data = System.Text.Json.JsonSerializer.Serialize(new {
                    eventType = e.EventType,
                    message = e.Message,
                    isUndo = e.IsUndo,
                    data = e.Data
                });
                var bytes = System.Text.Encoding.UTF8.GetBytes($"data: {data}\n\n");
                try {
                    Response.Body.WriteAsync(bytes, 0, bytes.Length).Wait();
                    Response.Body.FlushAsync().Wait();
                } catch {
                    tcs.Cancel();
                }
            }

            _monitor.EventReceived += OnEventReceived;

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
                _monitor.EventReceived -= OnEventReceived;
            }
        }
    }

    public class UtauEventArgs : EventArgs
    {
        public string EventType { get; set; }
        public string Message { get; set; }
        public bool IsUndo { get; set; }
        public object Data { get; set; }
    }

    public class EventsMonitor : ICmdSubscriber
    {
        private static readonly EventsMonitor _instance = new EventsMonitor();
        public static EventsMonitor Instance => _instance;

        public event EventHandler<UtauEventArgs> EventReceived;

        private EventsMonitor()
        {
            DocManager.Inst.AddSubscriber(this);
        }

        public void OnNext(UCommand cmd, bool isUndo)
        {
            string eventType = "project_changed";
            object eventData = null;

            if (cmd is UNotification notification)
            {
                if (notification is SetPlayPosTickNotification posTick)
                {
                    eventType = "play_pos_updated";
                    eventData = new { tick = posTick.playPosTick, pause = posTick.pause };
                }
                else if (notification is SeekPlayPosTickNotification seekTick)
                {
                    eventType = "play_pos_updated";
                    eventData = new { tick = seekTick.playPosTick, pause = seekTick.pause };
                }
                else if (notification is PhonemizedNotification)
                {
                    eventType = "phonemized";
                }
                else if (notification is PartRenderedNotification partRendered)
                {
                    eventType = "part_rendered";
                    eventData = new { part = partRendered.part?.name };
                }
                else if (notification is ProgressBarNotification progress)
                {
                    eventType = "render_progress";
                    eventData = new { progress = progress.Progress, info = progress.Info };
                }
                else
                {
                    eventType = "notification";
                    // other specific notifications like VolumeChange
                    if (notification is VolumeChangeNotification vol) {
                        eventType = "volume_changed";
                        eventData = new { trackNo = vol.TrackNo, volume = vol.Volume };
                    } else if (notification is PanChangeNotification pan) {
                        eventType = "pan_changed";
                        eventData = new { trackNo = pan.TrackNo, pan = pan.Pan };
                    } else if (notification is SoloTrackNotification solo) {
                        eventType = "track_solo";
                        eventData = new { trackNo = solo.trackNo, solo = solo.solo };
                    } else if (notification is FocusNoteNotification focusNote) {
                        eventType = "focus_note";
                        eventData = new { tick = focusNote.note?.position, lyric = focusNote.note?.lyric };
                    }
                }
            }
            else
            {
                // Note edits, part edits, add/remove, etc.
                eventType = "project_changed";
            }

            EventReceived?.Invoke(this, new UtauEventArgs {
                EventType = eventType,
                Message = cmd.ToString(),
                IsUndo = isUndo,
                Data = eventData
            });
        }
    }
}
