using System;
using System.IO;
using System.Drawing;
using YoloDetect;

namespace PfadfinderMain.ImageRecognition;

public static class TakePicture
{

    private static bool pyIsReady = true;
    private static bool directoriesCreated = false; // Cache to avoid repeated directory checks

    private static ArgumentParser _parser = new ArgumentParser([
        "-i",
        "imgrec/startimage.jpg",
        "-o",
        "imgrec/output",
        "-m",
        "assets/prendet_v4.onnx",
        "-p",
        "-j"
    ]);

    public static void SetUpPython()
    {
        if (pyIsReady)
        {
            Console.WriteLine("Python-Umgebung ist bereits eingerichtet.");
            return;
        }
        YoloDetect.PythonDetection.Setup.SetupPythonEnvironment();
        pyIsReady = true;
        Console.WriteLine("Python-Umgebung eingerichtet.");
        Console.WriteLine("Python-Umgebung bereit.");
    }

    /// <summary>
    /// Ensures all required directories for image processing exist
    /// Only creates directories if not already done to improve performance
    /// </summary>
    private static void EnsureDirectoryStructure()
    {
        if (directoriesCreated) return; // Skip if already created
        
        string[] requiredDirectories = {
            "imgrec",
            "imgrec/output",
            "imgrec/output/resized",
            "imgrec/output/detections",
            "imgrec/output/final"
        };

        foreach (string dir in requiredDirectories)
        {
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                Console.WriteLine($"Created directory: {dir}");
            }
        }
        
        directoriesCreated = true; // Mark as completed
    }

    public static void DetectAndSave(string imagePath)
    {
        if (!pyIsReady)
        {
            Console.Write("Warte auf Bildverarbeitung");
            while (!pyIsReady)
            {
                Console.Write(".");
                Thread.Sleep(100); // Reduced from 300ms to 100ms for faster feedback
                
                if (!pyIsReady)
                {
                    Thread.Sleep(100); // Reduced delay
                }
            }
            Console.WriteLine(); // Add a newline once ready
        }
        
        // Ensure all required directories exist before processing
        EnsureDirectoryStructure();
        
        YoloDetect.DetectObjects.Detect(_parser);
    }


    public static void CaptureImage()
    {
        string outputDirectory = "imgrec";
        string outputPath = Path.Combine(outputDirectory, "startimage.jpg");
        
        // Ensure the directory structure exists
        EnsureDirectoryStructure();
        
        Console.WriteLine($"Bild wird aufgenommen und unter {outputPath} gespeichert...");

        if (Program.getIsDummyRun())
        {
            Console.WriteLine("Dummy-Modus aktiviert. Bildaufnahme simuliert.");
            Thread.Sleep(100); // Reduced from 500ms to 100ms for faster testing
            
            // Copy a random test image from _testImages directory
            string testImagesDir = "assets/_testImages";
            if (Directory.Exists(testImagesDir))
            {
                string[] imageFiles = Directory.GetFiles(testImagesDir, "*.jpg");
                if (imageFiles.Length > 0)
                {
                    // Select a random image from the test images
                    Random random = new Random();
                    string randomImage = imageFiles[random.Next(imageFiles.Length)];
                    
                    try
                    {
                        File.Copy(randomImage, outputPath, true);
                        Console.WriteLine($"Test-Bild kopiert: {randomImage} -> {outputPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Fehler beim Kopieren des Test-Bildes: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine("Keine Test-Bilder gefunden in: " + testImagesDir);
                }
            }
            else
            {
                Console.WriteLine($"Test-Bilder Verzeichnis nicht gefunden: {testImagesDir}");
                Console.WriteLine("Bitte erstellen Sie das Verzeichnis und fügen Sie Testbilder hinzu.");
            }
        }
        else
        {
            try
            {
                // Using libcamera-still command for Raspberry Pi Camera Module 3
                // Options:
                // -o: output file
                // --width, --height: resolution (set to max quality for Camera Module 3)
                // --quality: JPEG quality (100 = highest)
                // --immediate: take picture immediately without preview
                // --nopreview: don't show preview window

                // Optimized camera settings for faster capture and processing
                // Reduced resolution for faster processing while maintaining sufficient quality for detection
                // Reduced JPEG quality for smaller file size and faster I/O
                string cameraCommand = $"libcamera-still -o {outputPath} --width 1920 --height 1080 --quality 85 --immediate --nopreview --timeout 1";

                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    Arguments = $"-c \"{cameraCommand}\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                Console.WriteLine("Starte Kamera-Aufnahme...");
                using (var process = System.Diagnostics.Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit();
                        string output = process.StandardOutput.ReadToEnd();

                        if (process.ExitCode != 0)
                        {
                            Console.WriteLine($"Kamera-Fehler (Exit-Code {process.ExitCode}): {output}");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Fehler: Der Kamera-Prozess konnte nicht gestartet werden.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler bei der Bildaufnahme: {ex.Message}");
                return;
            }
        }
        
        if (File.Exists(outputPath))
        {
            Console.WriteLine($"Bild erfolgreich aufgenommen und unter {outputPath} gespeichert.");
        }
        else
        {
            Console.WriteLine("Fehler: Bild konnte nicht gespeichert werden.");
        }
    }

    public static void ProcessImage()
    {
        // Simulate image processing
        Console.WriteLine("Bild wird verarbeitet...");
        if (Program.getIsDummyRun()) System.Threading.Thread.Sleep(500); // Reduced from 2000ms to 500ms for faster testing
        Console.WriteLine("Bildverarbeitung abgeschlossen.");
    }
}
