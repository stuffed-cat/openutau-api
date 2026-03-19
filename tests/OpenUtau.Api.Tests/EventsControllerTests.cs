using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using OpenUtau.Api.Controllers;
using OpenUtau.Core;

namespace OpenUtau.Api.Tests
{
    public class EventsControllerTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public EventsControllerTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task WebSocketEndpointSupportsPingPongAndEventPush()
        {
            var client = _factory.Server.CreateWebSocketClient();
            using var socket = await client.ConnectAsync(new Uri("ws://localhost/api/events/ws"), CancellationToken.None);

            var connected = await ReceiveJsonAsync(socket);
            Assert.Equal("connected", connected.GetProperty("type").GetString());

            await SendTextAsync(socket, "ping");
            var pong = await ReceiveJsonAsync(socket);
            Assert.Equal("pong", pong.GetProperty("type").GetString());

            EventsMonitor.Instance.OnNext(new ProgressBarNotification(0.42, "rendering"), false);

            var eventMessage = await ReceiveJsonAsync(socket);
            Assert.Equal("event", eventMessage.GetProperty("type").GetString());
            Assert.Equal("render_progress", eventMessage.GetProperty("eventType").GetString());
            Assert.Equal(0.42, eventMessage.GetProperty("data").GetProperty("progress").GetDouble(), 2);
            Assert.Equal("rendering", eventMessage.GetProperty("data").GetProperty("info").GetString());
        }

        [Fact]
        public async Task WebSocketBroadcastIsDeliveredToClients()
        {
            var client = _factory.Server.CreateWebSocketClient();
            using var socket = await client.ConnectAsync(new Uri("ws://localhost/api/events/ws"), CancellationToken.None);

            _ = await ReceiveJsonAsync(socket);

            await SendTextAsync(socket, JsonSerializer.Serialize(new
            {
                type = "broadcast",
                payload = new { message = "hello" }
            }));

            var broadcast = await ReceiveJsonAsync(socket);
            Assert.Equal("broadcast", broadcast.GetProperty("type").GetString());
            Assert.True(broadcast.TryGetProperty("clientId", out _));
            Assert.Equal("hello", broadcast.GetProperty("payload").GetProperty("message").GetString());
        }

        private static async Task SendTextAsync(WebSocket socket, string message)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private static async Task<JsonElement> ReceiveJsonAsync(WebSocket socket)
        {
            var buffer = new byte[4096];
            using var ms = new MemoryStream();

            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new InvalidOperationException("WebSocket closed before a message was received.");
                }

                ms.Write(buffer, 0, result.Count);

                if (result.EndOfMessage)
                {
                    break;
                }
            }

            using var doc = JsonDocument.Parse(ms.ToArray());
            return doc.RootElement.Clone();
        }
    }
}