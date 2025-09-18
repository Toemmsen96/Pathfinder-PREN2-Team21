using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace YoloDetect.ServiceClient
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
        /// Parse the detection response from the server
        /// </summary>
        private DetectionResult ParseDetectionResponse(JsonElement response)
        {
            var result = new DetectionResult();

            try
            {
                // Check if the response indicates success
                if (response.TryGetProperty("success", out var successProp))
                {
                    result.Success = successProp.GetBoolean();
                }
                else if (response.TryGetProperty("response", out var responseProp))
                {
                    result.Success = responseProp.GetString() == "ok";
                }

                // Get error message if present
                if (response.TryGetProperty("error", out var errorProp))
                {
                    result.ErrorMessage = errorProp.GetString() ?? "";
                }

                // Get the created JSON file paths
                if (response.TryGetProperty("json_files", out var jsonFilesProp))
                {
                    if (jsonFilesProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var filePath in jsonFilesProp.EnumerateArray())
                        {
                            if (filePath.ValueKind == JsonValueKind.String)
                            {
                                result.JsonFilePaths.Add(filePath.GetString() ?? "");
                            }
                        }
                    }
                }
                else if (response.TryGetProperty("json_file", out var jsonFileProp))
                {
                    // Single file path
                    var filePath = jsonFileProp.GetString();
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        result.JsonFilePaths.Add(filePath);
                    }
                }

                // Get detection count if provided
                if (response.TryGetProperty("detection_count", out var countProp))
                {
                    result.DetectionCount = countProp.GetInt32();
                }

                // Get output image paths if provided
                if (response.TryGetProperty("output_images", out var outputImagesProp))
                {
                    if (outputImagesProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var imagePath in outputImagesProp.EnumerateArray())
                        {
                            if (imagePath.ValueKind == JsonValueKind.String)
                            {
                                result.OutputImagePaths.Add(imagePath.GetString() ?? "");
                            }
                        }
                    }
                }
                else if (response.TryGetProperty("output_image", out var outputImageProp))
                {
                    // Single image path
                    var imagePath = outputImageProp.GetString();
                    if (!string.IsNullOrEmpty(imagePath))
                    {
                        result.OutputImagePaths.Add(imagePath);
                    }
                }

            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error parsing response: {ex.Message}";
            }

            return result;
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
        public List<string> JsonFilePaths { get; set; } = new();
        public List<string> OutputImagePaths { get; set; } = new();
        public int DetectionCount { get; set; }
        
        // Backward compatibility
        public int Count => DetectionCount;
        
        // For compatibility with existing code that expects Detections
        public List<DetectedObject> Detections { get; set; } = new();
    }
}