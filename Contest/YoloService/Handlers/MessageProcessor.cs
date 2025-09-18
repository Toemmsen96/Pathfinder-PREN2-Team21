using System;
using System.Text.Json;
using System.Threading.Tasks;
using YoloService.Services;

namespace YoloService.Handlers
{
    public static class MessageProcessor
    {
        public static async Task<string> ProcessMessage(string message, YoloDetectionService yoloService)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(message);
                var command = jsonDoc.RootElement.GetProperty("command").GetString();

                return command switch
                {
                    "ping" => JsonSerializer.Serialize(new { response = "pong", timestamp = DateTime.Now }),
                    "status" => JsonSerializer.Serialize(new
                    {
                        response = "ok",
                        server = "YoloService",
                        model_loaded = yoloService.IsModelLoaded,
                        timestamp = DateTime.Now
                    }),
                    "detect" => await HandleDetection(jsonDoc.RootElement, yoloService),
                    "echo" => JsonSerializer.Serialize(new
                    {
                        response = "echo",
                        data = jsonDoc.RootElement.GetProperty("data").GetString(),
                        timestamp = DateTime.Now
                    }),
                    _ => JsonSerializer.Serialize(new { response = "unknown_command", timestamp = DateTime.Now })
                };
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    response = "error",
                    message = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }

        private static async Task<string> HandleDetection(JsonElement element, YoloDetectionService yoloService)
        {
            try
            {
                var imagePath = element.GetProperty("image_path").GetString();
                var confidence = element.TryGetProperty("confidence", out var confProp) ? confProp.GetDouble() : 0.25;
                var outputPath = element.TryGetProperty("output_path", out var outProp) ? outProp.GetString() : "";
                var classes = element.TryGetProperty("classes", out var classesProp) ? classesProp.GetString() : "";
                var noDraw = element.TryGetProperty("no_draw", out var noDrawProp) && noDrawProp.GetBoolean();
                var saveJson = element.TryGetProperty("save_json", out var jsonProp) && jsonProp.GetBoolean();

                if (string.IsNullOrEmpty(imagePath))
                {
                    return JsonSerializer.Serialize(new
                    {
                        response = "error",
                        message = "image_path is required",
                        timestamp = DateTime.Now
                    });
                }
                if (outputPath == null)
                {
                    outputPath = ""; // Default to empty string if not provided
                }
                if (classes == null)
                {
                    classes = ""; // Default to empty string if not provided
                }

                var result = await yoloService.DetectAsync(imagePath, confidence, outputPath, classes, noDraw, saveJson);

                return JsonSerializer.Serialize(new
                {
                    response = "detection_complete",
                    result = result,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    response = "detection_error",
                    message = ex.Message,
                    timestamp = DateTime.Now
                });
            }
        }
    }
}