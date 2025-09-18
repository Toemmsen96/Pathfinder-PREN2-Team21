namespace YoloDetect{
    public class ArgumentParser{
        // Default parameter values
        internal string modelPath = Path.Combine(Directory.GetCurrentDirectory(),"models/prendet_v4.onnx");

        internal float confidenceThreshold = 0.4f;
        internal float iouThreshold = 0.7f;
        internal float nodeProximityThreshold = 3f; // percentage of image size

        internal bool verboseMode = false;
        internal string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
        internal bool outputJson = false;
        internal bool noDraw = false; // Option for skipping drawing
        internal bool pythonMode = false; // Flag for Python detection mode
        internal string inputPath = ""; // Input path for images or directories

        public ArgumentParser(string[] args)
        {
            // Constructor that takes command-line arguments
            ParseArguments(args);
        }
        public void ParseArguments(string[] args)
        {
            
            
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();
                
                switch (arg)
                {
                    case "-th":
                    case "--threshold":
                        if (i + 1 < args.Length && float.TryParse(args[i + 1], out float threshold))
                        {
                            nodeProximityThreshold = threshold;
                            Console.WriteLine($"Node proximity threshold set to: {nodeProximityThreshold}");
                            i++; // Skip the next argument as it's the value
                        }
                        break;
                        
                    case "-conf":
                    case "--confidence":
                        if (i + 1 < args.Length && float.TryParse(args[i + 1], out float conf))
                        {
                            confidenceThreshold = conf;
                            Console.WriteLine($"Confidence threshold set to: {confidenceThreshold}");
                            i++; // Skip the next argument as it's the value
                        }
                        break;
                        
                    case "-iou":
                    case "--iou-threshold":
                        if (i + 1 < args.Length && float.TryParse(args[i + 1], out float iou))
                        {
                            iouThreshold = iou;
                            Console.WriteLine($"IOU threshold set to: {iouThreshold}");
                            i++; // Skip the next argument as it's the value
                        }
                        break;
                        
                    case "-m":
                    case "--model":
                        if (i + 1 < args.Length)
                        {
                            modelPath = args[i + 1];
                            Console.WriteLine($"Model path set to: {modelPath}");
                            i++; // Skip the next argument as it's the value
                        }
                        break;
                    case "-v":
                    case "--verbose":
                        Console.WriteLine("Verbose mode enabled.");
                        verboseMode = true;
                        break;
                    case "-o":
                    case "--output":
                        if (i + 1 < args.Length)
                        {
                            outputDir = args[i + 1];
                            Console.WriteLine($"Output directory set to: {outputDir}");
                            i++; // Skip the next argument as it's the value
                        }
                        break;
                    case "-i":
                    case "--input":
                        if (i + 1 < args.Length)
                        {
                            inputPath = args[i + 1];
                            Console.WriteLine($"Input path set to: {inputPath}");
                            i++; // Skip the next argument as it's the value
                        }
                        break;
                    case "-j":
                    case "--json":
                        outputJson = true;
                        Console.WriteLine("JSON output enabled.");
                        break;
                    case "-nd":
                    case "--no-draw":
                        noDraw = true;
                        outputJson = true; // Implicitly enable JSON output when using no-draw
                        Console.WriteLine("No-draw mode enabled (skipping image annotation, JSON output only).");
                        break;
                    case "-p":
                    case "--python":
                        pythonMode = true;
                        Console.WriteLine("Python mode enabled.");
                        break;
                    case "-h":
                    case "--help":
                        PrintHelp();
                        break;
                    default:
                        // If not a flag, assume it's the input path (if not already set)
                        if (inputPath == "" && !arg.StartsWith("-"))
                        {
                            inputPath = args[i];
                        }
                        break;
                }
            }
            if (inputPath == "")
            {
                Console.WriteLine("No input path provided. Please specify an image or directory.");
                PrintHelp();
                throw new ArgumentException("Input path is required.");
            }
            else
            {
                Console.WriteLine($"Input path: {inputPath}");
                return;
            }
        }
        
        private static void PrintHelp()
        {
            Console.WriteLine("YOLOv11s Object Detection - Command Line Arguments");
            Console.WriteLine("================================================");
            Console.WriteLine("Usage: YoloDetect [OPTIONS] [INPUT_PATH]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -th, --threshold VALUE     Set node proximity threshold percentage (default: 3)");
            Console.WriteLine("  -conf, --confidence VALUE  Set confidence threshold (default: 0.25)");
            Console.WriteLine("  -iou, --iou-threshold VALUE Set IOU threshold (default: 0.7)");
            Console.WriteLine("  -m, --model PATH           Set model path");
            Console.WriteLine("  -v, --verbose              Enable verbose logging");
            Console.WriteLine("  -o, --output DIR           Set output directory");
            Console.WriteLine("  -i, --input PATH           Specify input path (alternative to positional argument)");
            Console.WriteLine("  -j, --json                 Enable JSON output of detection results");
            Console.WriteLine("  -nd, --no-draw             Skip image annotation, output JSON only");
            Console.WriteLine("  -p, --python               Use Python-based detection instead of .NET YOLO");
            Console.WriteLine("  -h, --help                 Show this help message");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  YoloDetect image.jpg");
            Console.WriteLine("  YoloDetect -th 40 -conf 0.3 images/");
            Console.WriteLine("  YoloDetect --model models/custom.onnx -o results/ image.jpg");
            Console.WriteLine("  YoloDetect -v -i images/ -o output/");
            Console.WriteLine("  YoloDetect --no-draw -i images/ -o results/");
            Console.WriteLine("  YoloDetect -p -i images/");
            Environment.Exit(0);
        }

    }
}