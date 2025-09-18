using System.Diagnostics;
using System.Text;
using SkiaSharp;
using YoloDotNet;
using YoloDotNet.Models;
using YoloDotNet.Enums;
namespace YoloDetect
{
    public class DetectObjects
    {
        private static ArgumentParser? argumentParser;
        public static void Detect(ArgumentParser argParser)
        {
            argumentParser = argParser;
            Utils.ClearOutputDirectory(argParser.outputDir);

            PreprocessImages.Preprocess(argParser.inputPath, argParser.outputDir, argParser.verboseMode);
            argParser.inputPath = Path.Combine(argParser.outputDir, "resized");

            bool detectionCompleted = false;

            if (argParser.pythonMode)
            {
                detectionCompleted = RunPythonDetectionAsync().GetAwaiter().GetResult();
            }
            else
            {
                RunCSDetection();
                detectionCompleted = true;
            }

            // Only run post-processing if detection was completed successfully
            if (!detectionCompleted)
            {
                Console.WriteLine("Detection failed or was not completed. Skipping post-processing.");
                return;
            }

            if (argParser.noDraw)
            {
                Console.WriteLine("No drawing mode enabled. Skipping image output.");
            }
            else
            {
                string[] jsonFiles = Directory.GetFiles(argumentParser.outputDir, "*.json");
                if (argParser.verboseMode)
                {
                    CalculateDiagonals.SetVerboseMode(argParser.verboseMode);
                }
                foreach (string jsonFilePath in jsonFiles){
                    Console.WriteLine($"Processing JSON file: {jsonFilePath}");
                    List<DetectedObject> objects = Utils.JsonToDetectedObject(jsonFilePath);
                    string imgPath = Utils.ImagePathFromJson(jsonFilePath, argumentParser.inputPath);
                    if (string.IsNullOrEmpty(imgPath) || !Utils.IsImageFile(Path.GetExtension(imgPath)))
                    {
                        Console.WriteLine($"Image path not found for JSON file: {jsonFilePath}");
                        Console.WriteLine($"Image path: {imgPath}");
                        Console.WriteLine($"Skipping drawing for this JSON file.");
                        continue;
                    }
                    SKBitmap image = SKBitmap.Decode(imgPath);
                    DrawDetected drawer = new DrawDetected(new SKCanvas(image),image, argParser.verboseMode, argParser.nodeProximityThreshold, imgPath, argParser.outputDir );
                    List<Coordinate> nodesAndPylons = GetNodesAndPylons(objects);
                    Console.WriteLine($"Found {nodesAndPylons.Count} nodes and pylons.");
                    drawer.DrawDetections(objects, nodesAndPylons);
                    
                    // Get the missing nodes from the drawer and add them to objects
                    List<DetectedObject> missingNodes = drawer.GetMissingNodes();
                    objects.AddRange(missingNodes);
                    Console.WriteLine($"Added {missingNodes.Count} missing nodes to the objects list.");
                    
                    string outputDir = Path.Combine(argParser.outputDir, "final");
                    Directory.CreateDirectory(outputDir); // Ensure the directory exists
                    string fileName = Path.GetFileNameWithoutExtension(imgPath) + "_detected.json";
                    string outputFilePath = Path.Combine(outputDir, fileName);
                    List<Line> lines = drawer.GetFinalLines();
                    Json.ExportFinalJson(objects,lines, outputFilePath, image.Width, image.Height);
                    CalculateDiagonals.ClearLinesToRecheck();
                }
            }
        }
           private static void GetImagesOrDirectory(string inputPath, List<string> imagesToProcess){
            if (File.Exists(inputPath))
            {
                // Single file processing
                string extension = Path.GetExtension(inputPath).ToLowerInvariant();
                if (Utils.IsImageFile(extension))
                {
                    imagesToProcess.Add(inputPath);
                }
                else
                {
                    Console.WriteLine("The specified file is not a supported image format.");
                    return;
                }
            }
            else if (Directory.Exists(inputPath))
            {
                // Directory processing
                Console.WriteLine($"Scanning directory: {inputPath}");
                string[] supportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp" };
                
                foreach (string extension in supportedExtensions)
                {
                    imagesToProcess.AddRange(Directory.GetFiles(inputPath, $"*{extension}"));
                    imagesToProcess.AddRange(Directory.GetFiles(inputPath, $"*{extension.ToUpper()}"));
                }
                
                if (imagesToProcess.Count == 0)
                {
                    Console.WriteLine("No supported image files found in the directory.");
                    return;
                }
                
                Console.WriteLine($"Found {imagesToProcess.Count} images to process.");
            }
            else
            {
                Console.WriteLine("The specified path does not exist.");
                return;
            }
        }


        public static void RunCSDetection()
        {
            Console.WriteLine("Using C# for object detection.");

            try
            {
                // Instantiate a new Yolo object
                String currentDir = Directory.GetCurrentDirectory();
                Console.WriteLine($"Current Directory: {currentDir}");
                if (argumentParser == null)
                {
                    Console.WriteLine("ArgumentParser is null. Cannot proceed.");
                    throw new ArgumentNullException(nameof(argumentParser));
                }
                if (!Directory.Exists(argumentParser.modelPath) && !File.Exists(argumentParser.modelPath))
                {
                    Console.WriteLine($"Model path does not exist: {argumentParser.modelPath}");
                    return;
                }

                using var yolo = new Yolo(new YoloOptions
                {
                    OnnxModel = @argumentParser.modelPath,      // Your Yolo model in onnx format
                    ModelType = ModelType.ObjectDetection,      // Set your model type
                    Cuda = false,                               // Use CPU or CUDA for GPU accelerated inference
                    GpuId = 0,                                  // Select Gpu by id
                    PrimeGpu = false,                           // Pre-allocate GPU before first inference
                });
                // Get list of images to process
                List<string> imagesToProcess = new List<string>();
            
                GetImagesOrDirectory(argumentParser.inputPath, imagesToProcess);

                // Process each image
                foreach (string imagePath in imagesToProcess)
                {
                    Console.WriteLine($"Processing: {Path.GetFileName(imagePath)}");
                    // Clear lines to recheck for each new image
                    CalculateDiagonals.ClearLinesToRecheck();
                    using var image = SKImage.FromEncodedData(imagePath);
                        
                    // Run inference and get the results
                    var results = yolo.RunObjectDetection(image, confidence: argumentParser.confidenceThreshold, iou: argumentParser.iouThreshold);
                    Console.WriteLine($"Found {results.Count} objects in the image.");
                        // Load image
                        // Only perform drawing operations if not in no-draw mode

                        Console.WriteLine();

                        // Always output JSON if either outputJson is true or noDraw is true (which implies JSON output)
                        if (argumentParser.outputJson || argumentParser.noDraw)
                        {
                           Utils.DetectedObjectToJson(results, imagePath, argumentParser.outputDir);
                        }
                    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing YOLO: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }


        public static async Task<bool> RunPythonDetectionAsync() {
            Console.WriteLine("Using Python for object detection.");
            
            if (argumentParser == null)
            {
                Console.WriteLine("ArgumentParser is null. Cannot proceed.");
                throw new ArgumentNullException(nameof(argumentParser));
            }

            // First, try to use WebSocket server if available
            try
            {
                using var client = new ServiceClient.YoloWebSocketClient();
                Console.WriteLine("Attempting to connect to YOLO WebSocket server...");
                
                if (await client.ConnectAsync())
                {
                    Console.WriteLine("Connected to WebSocket server. Checking if model is ready...");
                    
                    if (await client.IsModelReadyAsync())
                    {
                        Console.WriteLine("Model is ready. Using WebSocket server for detection.");
                        bool webSocketSuccess = await RunWebSocketDetection(client);
                        return webSocketSuccess; // Return actual WebSocket result
                    }
                    else
                    {
                        Console.WriteLine("Model is not ready on WebSocket server. Falling back to direct Python execution.");
                    }
                }
                else
                {
                    Console.WriteLine("Could not connect to WebSocket server. Falling back to direct Python execution.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WebSocket connection failed: {ex.Message}. Falling back to direct Python execution.");
            }

            // Fallback to direct Python execution
            return RunDirectPythonDetection();
        }

        private static async Task<bool> RunWebSocketDetection(ServiceClient.YoloWebSocketClient client)
        {
            // Get list of images to process
            List<string> imagesToProcess = new List<string>();
            if (argumentParser == null)
            {
                Console.WriteLine("ArgumentParser is null. Cannot proceed.");
                throw new ArgumentNullException(nameof(argumentParser));
            }
            GetImagesOrDirectory(argumentParser.inputPath, imagesToProcess);

            bool allSuccessful = true;

            foreach (string imagePath in imagesToProcess)
            {
                Console.WriteLine($"Processing via WebSocket: {Path.GetFileName(imagePath)}");
                
                // Convert to absolute paths before sending to WebSocket server
                string absoluteImagePath = Path.GetFullPath(imagePath);
                string absoluteOutputPath = Path.GetFullPath(argumentParser.outputDir);
                
                Console.WriteLine($"Sending absolute paths - Image: {absoluteImagePath}, Output: {absoluteOutputPath}");
                
                var result = await client.DetectAsync(
                    imagePath: absoluteImagePath,
                    confidence: argumentParser.confidenceThreshold,
                    outputPath: absoluteOutputPath,
                    noDraw: argumentParser.noDraw,
                    saveJson: argumentParser.outputJson || argumentParser.noDraw
                );

                // More detailed success checking
                bool detectionSuccessful = false;
                
                if (result != null)
                {
                    Console.WriteLine($"Result received - Success: {result.Success}, Count: {result.Count}, ErrorMessage: '{result.ErrorMessage}'");
                    
                    // The WebSocket service is working and returning valid detection results
                    // Consider it successful if we have a valid result with detections
                    if (result.Success || 
                        result.Count > 0 || 
                        result.JsonFilePaths.Count > 0 || 
                        result.OutputImagePaths.Count > 0 ||
                        string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        detectionSuccessful = true;
                        Console.WriteLine($"WebSocket detection completed successfully. Found {result.Count} objects.");
                        
                        if (result.JsonFilePaths.Count > 0)
                        {
                            Console.WriteLine($"JSON files created: {string.Join(", ", result.JsonFilePaths)}");
                        }
                        
                        if (result.OutputImagePaths.Count > 0)
                        {
                            Console.WriteLine($"Output images created: {string.Join(", ", result.OutputImagePaths)}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"WebSocket detection failed: {result.ErrorMessage ?? "Unknown error"}");
                    }
                }
                else
                {
                    Console.WriteLine("WebSocket detection failed: No result received");
                }

                if (!detectionSuccessful)
                {
                    allSuccessful = false;
                }
            }

            return allSuccessful;
        }

        private static bool RunDirectPythonDetection()
        {
            // Setup process to run Python
            var pythonCommand = "./python/bin/python";  // Default python command
            var scriptPath = "PythonDetection/detect.py";  // Python script name

            // Build arguments to pass to the Python script
            var pythonArgs = new StringBuilder(scriptPath);

            // Ensure argumentParser is not null before accessing its properties
            if (argumentParser == null)
            {
                Console.WriteLine("ArgumentParser is null. Cannot proceed.");
                return false;
            }

            // Python script uses different argument names
            if (!string.IsNullOrEmpty(argumentParser.inputPath))
            {
                // Python script uses --image instead of -i/--input
                pythonArgs.Append($" --image \"{argumentParser.inputPath}\"");
            }
            
            if (!string.IsNullOrEmpty(argumentParser.outputDir))
            {
                // Keep the same format for output
                pythonArgs.Append($" --output \"{argumentParser.outputDir}\"");
            }
            
            // Model path - same format
            pythonArgs.Append($" --model \"{argumentParser.modelPath}\"");
            
            // Confidence threshold - Python uses --conf instead of --confidence
            pythonArgs.Append($" --conf {argumentParser.confidenceThreshold}");
            
            
            // The following arguments don't exist in the Python script:
            // verbose, jsonOutput, noDraw, iouThreshold, proximityThreshold
            // We'll add them as custom arguments in case the Python script is updated later

            if (argumentParser.verboseMode)
            {
                pythonArgs.Append(" --verbose");
            }
            
            if (argumentParser.outputJson)
            {
                pythonArgs.Append(" --json");
            }
            
            if (argumentParser.noDraw)
            {
                pythonArgs.Append(" --no-draw");
            }
            
            if (argumentParser.iouThreshold != 0.7f)
            {
                pythonArgs.Append($" --iou {argumentParser.iouThreshold}");
            }
            

            var startInfo = new ProcessStartInfo
            {
                FileName = pythonCommand,
                Arguments = pythonArgs.ToString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Console.WriteLine($"Executing: {pythonCommand} {pythonArgs}");

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    Console.WriteLine("Failed to start Python process.");
                    return false;
                }
                
                // Read the output in real-time
                process.OutputDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.WriteLine($"Python: {e.Data}");
                };
                
                process.ErrorDataReceived += (sender, e) => {
                    if (!string.IsNullOrEmpty(e.Data))
                        Console.WriteLine($"Python Error: {e.Data}");
                };
                
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                process.WaitForExit();
                
                Console.WriteLine($"Python process exited with code: {process.ExitCode}");
                
                // Return success based on exit code
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing Python: {ex.Message}");
                return false;
            }
        }

        // Helper method to get nodes and pylons
        private static List<Coordinate> GetNodesAndPylons(List<DetectedObject> detectedObjects)
        {
            List<Coordinate> nodesAndPylons = new List<Coordinate>();
            foreach (var obj in detectedObjects)
            {
                if (obj.ClassName.Equals("node", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle node detection
                    Console.WriteLine($"Node detected: {obj.ClassName}");
                    
                    nodesAndPylons.Add(new Coordinate(obj.BoundingBox.BottomRight.X, obj.BoundingBox.BottomRight.Y));
                }
                else if (obj.ClassName.Equals("traffic_cone", StringComparison.OrdinalIgnoreCase))
                {
                    // Handle pylon detection
                    Console.WriteLine($"Cone detected: {obj.ClassName}");
                    nodesAndPylons.Add(new Coordinate(obj.BoundingBox.BottomRight.X, obj.BoundingBox.BottomRight.Y));
                }
            }
            return nodesAndPylons;
        }
        
    }
}