using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using YoloService.Services;

namespace YoloService.Handlers
{
    public static class WebSocketHandler
    {
        public static async Task HandleWebSocket(WebSocket webSocket, YoloDetectionService yoloService)
        {
            var buffer = new byte[1024 * 16];
            Console.WriteLine("WebSocket client connected");

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        Console.WriteLine($"Received: {message}");

                        var response = await MessageProcessor.ProcessMessage(message, yoloService);
                        Console.WriteLine($"Sending: {response}");

                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        await webSocket.SendAsync(
                            new ArraySegment<byte>(responseBytes),
                            WebSocketMessageType.Text,
                            true,
                            CancellationToken.None);
                        
                        Console.WriteLine("Response sent successfully");
                    }
                    else if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("WebSocket connection closed by client");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket error: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("WebSocket client disconnected");
            }
        }
    }
}