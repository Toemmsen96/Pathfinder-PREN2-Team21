using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;

namespace YoloService.Services
{
    public class YoloDetectionService : IDisposable
    {
        private readonly PythonEnvironmentManager _environmentManager;
        private readonly PythonScriptManager _scriptManager;
        private readonly string _modelPath;
        private Process? _pythonProcess;
        private StreamWriter? _processInput;
        private StreamReader? _processOutput;
        private readonly SemaphoreSlim _detectionSemaphore = new(1, 1);
        
        public bool IsModelLoaded { get; private set; }

        public YoloDetectionService()
        {
            _environmentManager = new PythonEnvironmentManager();
            _scriptManager = new PythonScriptManager();
            _modelPath = "models/prendet_v4.onnx";
        }

        public async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine("Initializing YOLO detection service...");

                // Setup Python environment
                if (!await _environmentManager.SetupEnvironmentAsync())
                {
                    Console.WriteLine("Failed to setup Python environment");
                    IsModelLoaded = false;
                    return;
                }

                // Create Python server script
                await _scriptManager.CreateDetectionServerScriptAsync(_modelPath);

                // Start persistent Python process
                if (await StartPythonServerAsync())
                {
                    IsModelLoaded = true;
                    Console.WriteLine("YOLO model loaded successfully in persistent process");
                }
                else
                {
                    IsModelLoaded = false;
                    Console.WriteLine("Failed to start Python server");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing YOLO service: {ex.Message}");
                IsModelLoaded = false;
            }
        }

        private async Task<bool> StartPythonServerAsync()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = _environmentManager.PythonExecutable,
                    Arguments = $"\"{_scriptManager.ServerScriptPath}\"",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                _pythonProcess = Process.Start(processInfo);
                if (_pythonProcess == null)
                    return false;

                _processInput = _pythonProcess.StandardInput;
                _processOutput = _pythonProcess.StandardOutput;

                // Wait for READY signal
                var readyTask = Task.Run(async () =>
                {
                    string? line;
                    while ((line = await _pythonProcess.StandardError.ReadLineAsync()) != null)
                    {
                        Console.WriteLine($"Python: {line}");
                        if (line.Contains("READY"))
                            return true;
                    }
                    return false;
                });

                var timeoutTask = Task.Delay(30000); // 30 second timeout
                var completedTask = await Task.WhenAny(readyTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    Console.WriteLine("Timeout waiting for Python server to start");
                    return false;
                }

                return await readyTask;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting Python server: {ex.Message}");
                return false;
            }
        }

        public async Task<object> DetectAsync(string imagePath, double confidence, string outputPath, string classes, bool noDraw, bool saveJson)
        {
            if (!IsModelLoaded || _processInput == null || _processOutput == null)
            {
                throw new InvalidOperationException("YOLO model is not loaded or Python process is not running");
            }

            await _detectionSemaphore.WaitAsync();
            try
            {
                var request = new
                {
                    image_path = imagePath,
                    conf = confidence,
                    output_path = outputPath ?? "",
                    classes = classes ?? "",
                    no_draw = noDraw,
                    save_json = saveJson
                };

                var requestJson = JsonSerializer.Serialize(request);
                
                // Send request to Python process
                await _processInput.WriteLineAsync(requestJson);
                await _processInput.FlushAsync();

                // Read response
                var responseJson = await _processOutput.ReadLineAsync();
                if (string.IsNullOrEmpty(responseJson))
                {
                    throw new Exception("No response from Python process");
                }

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                };

                return JsonSerializer.Deserialize<JsonElement>(responseJson, options);
            }
            finally
            {
                _detectionSemaphore.Release();
            }
        }

        public void Dispose()
        {
            try
            {
                // Send exit command to Python process
                _processInput?.WriteLine("EXIT");
                _processInput?.Flush();
                
                // Wait a bit for graceful shutdown
                _pythonProcess?.WaitForExit(5000);
                
                // Force kill if still running
                if (_pythonProcess?.HasExited == false)
                {
                    _pythonProcess.Kill();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing YOLO service: {ex.Message}");
            }
            finally
            {
                _processInput?.Dispose();
                _processOutput?.Dispose();
                _pythonProcess?.Dispose();
                _detectionSemaphore.Dispose();
            }
        }
    }
}