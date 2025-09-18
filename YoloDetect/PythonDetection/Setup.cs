using System.Runtime.InteropServices;

namespace YoloDetect.PythonDetection{
    public class Setup{

        public static string pythonPath = Path.Combine(Directory.GetCurrentDirectory(), "python");
        public static void SetupPythonEnvironment()
        {
            // Check if Python is installed
            if (!File.Exists(pythonPath))
            {
            try
            {
                Console.WriteLine("Setting up Python virtual environment...");
                
                // Create Python virtual environment
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = "-m venv python",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };
                
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"Failed to create Python environment: {error}");
                    Environment.Exit(1);
                }
                
                Console.WriteLine("Installing required Python packages...");
                
                // Install required packages using pip, but first check if they're already installed
                Console.WriteLine("Checking for required Python packages...");

                // Determine if we're running on ARM architecture
                bool isArmArchitecture = RuntimeInformation.ProcessArchitecture == Architecture.Arm ||
                                         RuntimeInformation.ProcessArchitecture == Architecture.Arm64;
                
                // Exclude onnxruntime-gpu for ARM architectures
                string[] requiredPackages = isArmArchitecture 
                    ? new[] { "ultralytics", "onnx", "onnxruntime" }
                    : new[] { "ultralytics", "onnx", "onnxruntime", "onnxruntime-gpu" };
                
                if (isArmArchitecture)
                {
                    Console.WriteLine("ARM architecture detected. Using CPU-only ONNX runtime.");
                }
                
                List<string> packagesToInstall = new List<string>();

                foreach (string package in requiredPackages)
                {
                    var checkProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = Path.Combine(pythonPath, "bin", "pip"),
                            Arguments = $"show {package}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    checkProcess.Start();
                    checkProcess.WaitForExit();
                    
                    if (checkProcess.ExitCode != 0)
                    {
                        Console.WriteLine($"Package {package} not found, will install.");
                        packagesToInstall.Add(package);
                    }
                    else
                    {
                        Console.WriteLine($"Package {package} already installed.");
                    }
                }

                if (packagesToInstall.Count > 0)
                {
                    Console.WriteLine($"Installing missing packages: {string.Join(", ", packagesToInstall)}");
                    
                    var pipProcess = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = Path.Combine(pythonPath, "bin", "pip"),
                            Arguments = $"install -U {string.Join(" ", packagesToInstall)}",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        }
                    };
                    
                    pipProcess.Start();
                    string pipOutput = pipProcess.StandardOutput.ReadToEnd();
                    string pipError = pipProcess.StandardError.ReadToEnd();
                    pipProcess.WaitForExit();
                    
                    if (pipProcess.ExitCode != 0)
                    {
                        Console.WriteLine($"Failed to install Python packages: {pipError}");
                        Environment.Exit(1);
                    }
                    else
                    {
                        Console.WriteLine("Successfully installed missing packages.");
                    }
                }
                else
                {
                    Console.WriteLine("All required Python packages are already installed.");
                }
                
                Console.WriteLine("Python environment set up successfully");
                
                // Set up any necessary environment variables or paths for Python
                Environment.SetEnvironmentVariable("PYTHONPATH", pythonPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting up Python environment: {ex.Message}");
                Environment.Exit(1);
            }
            }
            else
            {
                Console.WriteLine("Python virtual environment already exists.");
            }
        }

    }
}