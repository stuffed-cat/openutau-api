using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
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
        private static readonly ConcurrentDictionary<Guid, WebSocketConnection> WebSocketClients = new ConcurrentDictionary<Guid, WebSocketConnection>();
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

        [HttpGet("ws")]
        public async Task WebSocketEvents()
        {
            if (!HttpContext.WebSockets.IsWebSocketRequest)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                await Response.WriteAsync("WebSocket request expected.");
                return;
            }

            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            var client = new WebSocketConnection(webSocket);
            WebSocketClients[client.Id] = client;

            void OnEventReceived(object sender, UtauEventArgs e)
            {
                _ = client.SendAsync(new
                {
                    type = "event",
                    eventType = e.EventType,
                    message = e.Message,
                    isUndo = e.IsUndo,
                    data = e.Data
                }, HttpContext.RequestAborted);
            }

            _monitor.EventReceived += OnEventReceived;

            try
            {
                await client.SendAsync(new { type = "connected" }, HttpContext.RequestAborted);
                await ReceiveLoop(client, HttpContext.RequestAborted);
            }
            finally
            {
                _monitor.EventReceived -= OnEventReceived;
                WebSocketClients.TryRemove(client.Id, out _);
                await client.CloseAsync();
            }
        }

        private static async Task ReceiveLoop(WebSocketConnection client, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            while (!cancellationToken.IsCancellationRequested && client.Socket.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                var message = new StringBuilder();
                do
                {
                    result = await client.Socket.ReceiveAsync(buffer, cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                } while (!result.EndOfMessage);

                await HandleClientMessage(client, message.ToString(), cancellationToken);
            }
        }

        private static async Task HandleClientMessage(WebSocketConnection client, string message, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            try
            {
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;

                if (string.Equals(type, "ping", StringComparison.OrdinalIgnoreCase))
                {
                    await client.SendAsync(new { type = "pong" }, cancellationToken);
                    return;
                }

                if (string.Equals(type, "broadcast", StringComparison.OrdinalIgnoreCase))
                {
                    var payload = root.TryGetProperty("payload", out var payloadProp) ? payloadProp : default;
                    await BroadcastAsync(new
                    {
                        type = "broadcast",
                        clientId = client.Id,
                        payload = payload.ValueKind == JsonValueKind.Undefined ? null : JsonSerializer.Deserialize<object>(payload.GetRawText())
                    }, cancellationToken);
                    return;
                }

                await client.SendAsync(new { type = "error", message = "Unsupported websocket message type." }, cancellationToken);
            }
            catch (JsonException)
            {
                if (string.Equals(message.Trim(), "ping", StringComparison.OrdinalIgnoreCase))
                {
                    await client.SendAsync(new { type = "pong" }, cancellationToken);
                }
                else
                {
                    await client.SendAsync(new { type = "error", message = "Invalid websocket message format." }, cancellationToken);
                }
            }
        }

        private static async Task BroadcastAsync(object payload, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            foreach (var connection in WebSocketClients.Values)
            {
                tasks.Add(connection.SendAsync(payload, cancellationToken));
            }
            await Task.WhenAll(tasks);
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

    internal sealed class WebSocketConnection
    {
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        public Guid Id { get; } = Guid.NewGuid();
        public WebSocket Socket { get; }

        public WebSocketConnection(WebSocket socket)
        {
            Socket = socket;
        }

        public async Task SendAsync(object payload, CancellationToken cancellationToken)
        {
            if (Socket.State != WebSocketState.Open)
            {
                return;
            }

            var json = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                if (Socket.State == WebSocketState.Open)
                {
                    await Socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async Task CloseAsync()
        {
            try
            {
                if (Socket.State == WebSocketState.Open)
                {
                    await Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
            }
            catch
            {
                // Ignore close failures
            }
            finally
            {
                Socket.Dispose();
                _sendLock.Dispose();
            }
        }
    }
}
