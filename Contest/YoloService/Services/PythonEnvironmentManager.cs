using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace YoloService.Services
{
    public class PythonEnvironmentManager
    {
        private readonly string _baseDirectory;
        private readonly string _venvPath;
        private readonly string _pythonExecutable;
        private readonly string _pipExecutable;
        private readonly string[] _requiredPackages = {
            "ultralytics",
            "torch",
            "torchvision",
            "opencv-python",
            "pillow",
            "numpy",
            "onnxruntime",
            "onnx"
        };

        public PythonEnvironmentManager()
        {
            _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _venvPath = Path.Combine(_baseDirectory, "python_venv");
            _pythonExecutable = Path.Combine(_venvPath, "bin", "python");
            _pipExecutable = Path.Combine(_venvPath, "bin", "pip");
        }

        public string PythonExecutable => _pythonExecutable;

        public async Task<bool> SetupEnvironmentAsync()
        {
            try
            {
                Console.WriteLine("Setting up Python environment...");

                // Create virtual environment if it doesn't exist
                if (!Directory.Exists(_venvPath))
                {
                    Console.WriteLine("Creating virtual environment...");
                    if (!await CreateVirtualEnvironmentAsync())
                    {
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine("Virtual environment already exists");
                }

                // Verify virtual environment is working
                if (!await VerifyVirtualEnvironmentAsync())
                {
                    Console.WriteLine("Virtual environment verification failed, recreating...");
                    Directory.Delete(_venvPath, true);
                    if (!await CreateVirtualEnvironmentAsync())
                    {
                        return false;
                    }
                }

                // Upgrade pip first
                Console.WriteLine("Upgrading pip...");
                await UpgradePipAsync();

                // Check and install required packages
                Console.WriteLine("Checking Python dependencies...");
                if (!await CheckAndInstallPackagesAsync())
                {
                    return false;
                }

                Console.WriteLine("Python environment ready!");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting up Python environment: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> CreateVirtualEnvironmentAsync()
        {
            // Try python3 first, then python
            var pythonCommands = new[] { "python3", "python" };
            
            foreach (var pythonCmd in pythonCommands)
            {
                Console.WriteLine($"Trying to create venv with {pythonCmd}...");
                
                var processInfo = new ProcessStartInfo
                {
                    FileName = pythonCmd,
                    Arguments = $"-m venv \"{_venvPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processInfo);
                if (process == null)
                {
                    Console.WriteLine($"Failed to start {pythonCmd} process");
                    continue;
                }

                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"Virtual environment created successfully with {pythonCmd}");
                    return true;
                }
                else
                {
                    var error = await process.StandardError.ReadToEndAsync();
                    Console.WriteLine($"Failed to create virtual environment with {pythonCmd}: {error}");
                }
            }

            Console.WriteLine("Failed to create virtual environment with any Python command");
            return false;
        }

        private async Task<bool> VerifyVirtualEnvironmentAsync()
        {
            if (!File.Exists(_pythonExecutable))
            {
                Console.WriteLine("Python executable not found in virtual environment");
                return false;
            }

            var processInfo = new ProcessStartInfo
            {
                FileName = _pythonExecutable,
                Arguments = "--version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null) return false;

            await process.WaitForExitAsync();
            
            if (process.ExitCode == 0)
            {
                var version = await process.StandardOutput.ReadToEndAsync();
                Console.WriteLine($"Virtual environment Python version: {version.Trim()}");
                return true;
            }

            return false;
        }

        private async Task<bool> UpgradePipAsync()
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = _pythonExecutable,
                Arguments = "-m pip install --upgrade pip",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                Console.WriteLine("Failed to start pip upgrade process");
                return false;
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                Console.WriteLine($"Pip upgrade warning: {error}");
                // Don't fail on pip upgrade errors as it's not critical
            }

            return true;
        }

        private async Task<bool> CheckAndInstallPackagesAsync()
        {
            foreach (var package in _requiredPackages)
            {
                if (!await IsPackageInstalledAsync(package))
                {
                    Console.WriteLine($"Installing {package}...");
                    if (!await InstallPackageAsync(package))
                    {
                        return false;
                    }
                }
                else
                {
                    Console.WriteLine($"✓ {package} is already installed");
                }
            }

            return true;
        }

        private async Task<bool> IsPackageInstalledAsync(string packageName)
        {
            var importName = GetImportName(packageName);
            var processInfo = new ProcessStartInfo
            {
                FileName = _pythonExecutable,
                Arguments = $"-c \"import {importName}; print('OK')\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null) return false;

            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }

        private async Task<bool> InstallPackageAsync(string packageName)
        {
            // Use python -m pip instead of direct pip to ensure we're using the venv pip
            var processInfo = new ProcessStartInfo
            {
                FileName = _pythonExecutable,
                Arguments = $"-m pip install {packageName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                Console.WriteLine($"Failed to start pip install process for {packageName}");
                return false;
            }

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var error = await process.StandardError.ReadToEndAsync();
                var output = await process.StandardOutput.ReadToEndAsync();
                Console.WriteLine($"Failed to install {packageName}:");
                Console.WriteLine($"Error: {error}");
                Console.WriteLine($"Output: {output}");
                return false;
            }

            Console.WriteLine($"✓ {packageName} installed successfully");
            return true;
        }

        private static string GetImportName(string packageName)
        {
            return packageName switch
            {
                "opencv-python" => "cv2",
                "pillow" => "PIL",
                _ => packageName
            };
        }
    }
}