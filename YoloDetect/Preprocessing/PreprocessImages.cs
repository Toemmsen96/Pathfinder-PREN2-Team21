using SkiaSharp;

namespace YoloDetect
{
    public class PreprocessImages
    {
        public static void Preprocess(string inputPath, string outputPath, bool verbose = false)
        {
            if (verbose)
            {
                Console.WriteLine($"Preprocessing images from {inputPath} to {outputPath}");
            }
            if (File.Exists(inputPath))
            {
                if (verbose) Console.WriteLine($"Processing file: {inputPath}");
                
                // Get the extension and check if it's an image file
                string extension = Path.GetExtension(inputPath);
                if (!Utils.IsImageFile(extension)) 
                {
                    if (verbose) Console.WriteLine($"Skipping non-image file: {inputPath}");
                    return;
                }
                
                try
                {
                    // Load the image
                    using var inputImage = SKBitmap.Decode(inputPath);
                    
                    // Resize the image
                    var resizedImage = ImageResizer.ResizeTo640x640(inputImage);
                    
                    // Save the resized image
                    string resizedDir = Path.Combine(outputPath, "resized");
                    Directory.CreateDirectory(resizedDir);
                    var outputFileName = Path.Combine(resizedDir, Path.GetFileName(inputPath));

                    using var outputStream = File.OpenWrite(outputFileName);
                    resizedImage.Encode(outputStream, SKEncodedImageFormat.Jpeg, 100);
                    if (verbose) Console.WriteLine($"Resized image saved to {outputFileName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing image {inputPath}: {ex.Message}");
                }
            }
            else if (Directory.Exists(inputPath))
            {
                foreach (var file in Directory.GetFiles(inputPath))
                {
                    if (verbose) Console.WriteLine($"Processing file: {file}");
                    
                    // Get the extension and check if it's an image file
                    string extension = Path.GetExtension(file);
                    if (!Utils.IsImageFile(extension)) continue;
                    
                    try
                    {
                        // Load the image
                        using var inputImage = SKBitmap.Decode(file);
                        
                        // Resize the image
                        var resizedImage = ImageResizer.ResizeTo640x640(inputImage);
                        
                        // Save the resized image
                        string resizedDir = Path.Combine(outputPath, "resized");
                        Directory.CreateDirectory(resizedDir);
                        var outputFileName = Path.Combine(resizedDir, Path.GetFileName(file));

                        using var outputStream = File.OpenWrite(outputFileName);
                        resizedImage.Encode(outputStream, SKEncodedImageFormat.Jpeg, 100);
                        if (verbose) Console.WriteLine($"Resized image saved to {outputFileName}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing image {file}: {ex.Message}");
                    }
                }
            }
            else
            {
                throw new FileNotFoundException($"Input path '{inputPath}' does not exist.");
            }
        }
    }
}