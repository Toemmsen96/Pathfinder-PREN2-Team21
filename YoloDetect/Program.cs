using YoloDotNet;
using YoloDotNet.Enums;
using YoloDotNet.Models;
using YoloDotNet.Extensions;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace YoloDetect
{
    class Program
    {
        
        private static List<Line> drawnLines = new List<Line>();
        
        
        private static string pythonPath = Path.Combine(Directory.GetCurrentDirectory(), "python");

        private static ArgumentParser? argumentParser;

        static void Main(string[] args)
        {
            Console.WriteLine("YOLOv11s Object Detection");
            Console.WriteLine("========================");
            argumentParser = new ArgumentParser(args);
            if (argumentParser == null)
            {
                Console.WriteLine("Failed to parse command line arguments.");
                return;
            }
            
            
            
            // If Python mode is enabled, run Python detection and exit
            if (argumentParser.pythonMode)
            {
                //PythonDetection.Setup.SetupPythonEnvironment();
                Console.WriteLine("Running in Python mode. Using Python-based detection.");
                // Pass all command-line arguments to DetectObjects.Detect()
                DetectObjects.Detect(argumentParser);
                return; // TODO: Handle exit after Python detection
            }
            else
            {
                Console.WriteLine("Running in C# mode. Using C#-based detection.");
                DetectObjects.Detect(argumentParser);

            }
            
            Directory.CreateDirectory(argumentParser.outputDir);
            
            // Configure CalculateDiagonals with current verbose setting
            CalculateDiagonals.SetVerboseMode(argumentParser.verboseMode);

            SetInputPath(ref argumentParser.inputPath);

        }

        private static void SetInputPath(ref string inputPath){
             if (string.IsNullOrEmpty(inputPath))
            {
                Console.WriteLine("No input provided. Please enter a path to an image or directory:");
                inputPath = Console.ReadLine()?.Trim('"') ?? string.Empty;
                if (string.IsNullOrEmpty(inputPath))
                {
                    Console.WriteLine("No input path provided. Exiting.");
                    return;
                }
            }
        }

    }
}