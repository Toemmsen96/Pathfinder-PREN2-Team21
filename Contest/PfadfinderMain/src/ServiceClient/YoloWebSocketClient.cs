using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace ServiceClient
{
    public class YoloWebSocketClient : IDisposable
    {
        private ClientWebSocket? _webSocket;
        private readonly string _serverUri;
        private bool _isConnected;

        public YoloWebSocketClient(string serverUri = "ws://localhost:5000/ws")
        {
            _serverUri = serverUri;
        }

        public bool IsConnected => _isConnected && _webSocket?.State == WebSocketState.Open;

        /// <summary>
        /// Connect to the YOLO WebSocket server
        /// </summary>
        public async Task<bool> ConnectAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(new Uri(_serverUri), cancellationToken);
                _isConnected = true;
                Console.WriteLine("Connected to YOLO WebSocket server");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Check if the YOLO model is ready
        /// </summary>
        public async Task<bool> IsModelReadyAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to server");
            }

            try
            {
                var statusRequest = new { command = "status" };
                var response = await SendRequestAsync(statusRequest, cancellationToken);

                if (response != null &&
                    response.Value.TryGetProperty("response", out var responseType) &&
                    responseType.GetString() == "ok" &&
                    response.Value.TryGetProperty("model_loaded", out var modelLoaded))
                {
                    return modelLoaded.GetBoolean();
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking model status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Perform object detection on an image
        /// </summary>
        public async Task<DetectionResult?> DetectAsync(
            string imagePath,
            double confidence = 0.25,
            string outputPath = "",
            string classes = "",
            bool noDraw = false,
            bool saveJson = false,
            CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("Not connected to server");
            }

            try
            {
                var detectRequest = new
                {
                    command = "detect",
                    image_path = imagePath,
                    confidence = confidence,
                    output_path = outputPath,
                    classes = classes,
                    no_draw = noDraw,
                    save_json = saveJson
                };

                var response = await SendRequestAsync(detectRequest, cancellationToken);

                if (response != null)
                {
                    return ParseDetectionResponse(response.Value);
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during detection: {ex.Message}");
                return new DetectionResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// Ping the server to check connectivity
        /// </summary>
        public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                return false;
            }

            try
            {
                var pingRequest = new { command = "ping" };
                var response = await SendRequestAsync(pingRequest, cancellationToken);

                return response != null &&
                       response.Value.TryGetProperty("response", out var responseType) &&
                       responseType.GetString() == "pong";
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Send a request and wait for response
        /// </summary>
        private async Task<JsonElement?> SendRequestAsync(object request, CancellationToken cancellationToken)
        {
            if (_webSocket == null || !IsConnected)
            {
                throw new InvalidOperationException("WebSocket not connected");
            }

            // Send request
            var requestJson = JsonSerializer.Serialize(request);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);

            await _webSocket.SendAsync(
                new ArraySegment<byte>(requestBytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken);

            // Receive response
            var buffer = new byte[1024 * 16];
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                var responseJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                var document = JsonDocument.Parse(responseJson);
                return document.RootElement;
            }

            return null;
        }

        /// <summary>
        /// Parse detection response from server
        /// </summary>
        private static DetectionResult ParseDetectionResponse(JsonElement response)
        {
            var result = new DetectionResult();

            if (response.TryGetProperty("response", out var responseType))
            {
                var responseStr = responseType.GetString();

                if (responseStr == "detection_complete")
                {
                    result.Success = true;

                    if (response.TryGetProperty("result", out var resultData) &&
                        resultData.TryGetProperty("detections", out var detections))
                    {
                        foreach (var detection in detections.EnumerateArray())
                        {
                            var det = new Detection();

                            if (detection.TryGetProperty("bounding_box", out var bbox))
                            {
                                det.BoundingBox = new BoundingBox
                                {
                                    Left = bbox.GetProperty("left").GetInt32(),
                                    Top = bbox.GetProperty("top").GetInt32(),
                                    Right = bbox.GetProperty("right").GetInt32(),
                                    Bottom = bbox.GetProperty("bottom").GetInt32()
                                };
                            }

                            if (detection.TryGetProperty("confidence", out var conf))
                                det.Confidence = conf.GetDouble();

                            if (detection.TryGetProperty("class_name", out var className))
                                det.ClassName = className.GetString() ?? "";

                            if (detection.TryGetProperty("class_id", out var classId))
                                det.ClassId = classId.GetInt32();

                            if (detection.TryGetProperty("detection_id", out var detId))
                                det.DetectionId = detId.GetString() ?? "";

                            result.Detections.Add(det);
                        }
                    }
                }
                else if (responseStr == "detection_error")
                {
                    result.Success = false;
                    if (response.TryGetProperty("message", out var message))
                    {
                        result.ErrorMessage = message.GetString() ?? "Unknown error";
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Disconnect from the server
        /// </summary>
        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_webSocket != null && IsConnected)
            {
                try
                {
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during disconnect: {ex.Message}");
                }
                finally
                {
                    _isConnected = false;
                }
            }
        }

        public void Dispose()
        {
            if (_webSocket != null)
            {
                if (IsConnected)
                {
                    try
                    {
                        _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", CancellationToken.None).Wait(1000);
                    }
                    catch { }
                }
                _webSocket.Dispose();
            }
        }
    }

    /// <summary>
    /// Result of a detection operation
    /// </summary>
    public class DetectionResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = "";
        public List<Detection> Detections { get; set; } = new();
        public int Count => Detections.Count;
    }

    /// <summary>
    /// Individual detection result
    /// </summary>
    public class Detection
    {
        public BoundingBox BoundingBox { get; set; } = new();
        public double Confidence { get; set; }
        public string ClassName { get; set; } = "";
        public int ClassId { get; set; }
        public string DetectionId { get; set; } = "";
    }

    /// <summary>
    /// Bounding box coordinates
    /// </summary>
    public class BoundingBox
    {
        public int Left { get; set; }
        public int Top { get; set; }
        public int Right { get; set; }
        public int Bottom { get; set; }

        public int Width => Right - Left;
        public int Height => Bottom - Top;

        public override string ToString()
        {
            return $"({Left}, {Top}, {Right}, {Bottom}) [{Width}x{Height}]";
        }
    }
}