

using System.Net.Http.Json;
using System.Text.Json;
using YoloDotNet.Models;

namespace YoloDetect{
    public static class Utils{
        public static List<DetectedObject> JsonToDetectedObject(string path)
        {
            string json = File.ReadAllText(path);
            Console.WriteLine($"Parsing JSON from: {path}");
            Console.Write(json);
            Console.WriteLine();
            List<DetectedObject> detectedObjects = new List<DetectedObject>();
            try
            {
                var jsonDocument = JsonDocument.Parse(json);
                var detectionsElement = jsonDocument.RootElement.GetProperty("detections");
                
                foreach (var detection in detectionsElement.EnumerateArray())
                {
                    var boundingBoxElement = detection.GetProperty("bounding_box");
                    var left = boundingBoxElement.GetProperty("left").GetSingle();
                    var top = boundingBoxElement.GetProperty("top").GetSingle();
                    var right = boundingBoxElement.GetProperty("right").GetSingle();
                    var bottom = boundingBoxElement.GetProperty("bottom").GetSingle();
                    
                    var confidence = detection.GetProperty("confidence").GetSingle();
                    var className = detection.GetProperty("class_name").GetString();
                    var classId = detection.GetProperty("class_id").GetInt32();
                    var detectionId = detection.GetProperty("detection_id").GetString();
                    if (className == null || detectionId == null)
                    {
                       throw new Exception("Shizzle is null");
                    }
                    
                    var detectedObject = new DetectedObject(
                        classId,
                        confidence,
                        new BoundingBox(
                            new Coordinate(left, top),
                            new Coordinate(right, top),
                            new Coordinate(left, bottom),
                            new Coordinate(right, bottom)
                        ),
                        className,
                        detectionId
                    );
                    
                    detectedObjects.Add(detectedObject);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing JSON: {ex.Message}");
            }
            return detectedObjects;
        }

        public static void DetectedObjectToJson(List<ObjectDetection> detectedObjects, string imagePath, string outputPath)
        {
             
            var detectionJson = new
            {
                detections = detectedObjects.Select(r => new
                {
                    bounding_box = new
                    {
                        left = r.BoundingBox.Left,
                        top = r.BoundingBox.Top,
                        right = r.BoundingBox.Right,
                        bottom = r.BoundingBox.Bottom
                    },
                    confidence = r.Confidence,
                    class_name = r.Label.Name,
                    class_id = GetClassId(r.Label.Name),
                    detection_id = Guid.NewGuid().ToString()
                }).ToList()
            };
            
            // Save to JSON file
            string jsonFileName = Path.GetFileNameWithoutExtension(imagePath) + "_detection.json";
            string jsonOutputPath = Path.Combine(outputPath, jsonFileName);
            var options = new JsonSerializerOptions { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            File.WriteAllText(jsonOutputPath, JsonSerializer.Serialize(detectionJson, options));
            
            Console.WriteLine($"JSON output saved to: {jsonOutputPath}");
        }
        public static bool IsImageFile(string extension)
        {
            string[] supportedFormats = { ".jpg", ".jpeg", ".png", ".bmp" };
            return supportedFormats.Contains(extension.ToLowerInvariant());
        }
        // Helper method to get class ID from name
        public static int GetClassId(string className)
        {
            // Match the class IDs from model_result.json
            return className.ToLower() switch
            {
                "line" => 0,
                "node" => 1,
                "pylon" => 2,
                "items" => 3,
                _ => 99 // Default for unknown classes
            };
        }

        public static string ImagePathFromJson(string jsonPath, string inputPath)
        {
            if (File.Exists(inputPath))
            {
                return inputPath;
            }
            string tempImagePath = Path.GetFileName(jsonPath).Replace("_detection.json", "");
            string imagePath = Path.Combine(inputPath, tempImagePath);
            // Try to find the image file with a supported extension
            foreach (var extension in new[] { ".jpg", ".jpeg", ".png", ".bmp" })
            {
                Console.WriteLine($"Checking for image file: {imagePath + extension}");
                string candidatePath = imagePath + extension;
                if (File.Exists(candidatePath))
                {
                    Console.WriteLine($"Found image file: {candidatePath}");
                    return candidatePath;
                }
            }
            return tempImagePath;
        }

        public static void ClearOutputDirectory(string outputDir)
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
                Directory.CreateDirectory(outputDir);
                Console.WriteLine($"Cleared output directory: {outputDir}");

            }
            else
            {
                Directory.CreateDirectory(outputDir);
                Console.WriteLine($"Created output directory: {outputDir}");
            }
        }

    }

}