using SkiaSharp;

namespace YoloDetect
{
    public class ImageResizer
    {
        public static SKBitmap ResizeImage(SKBitmap image, int newWidth, int newHeight)
        {
            // Create a new bitmap with the specified dimensions
            SKBitmap resizedImage = new SKBitmap(newWidth, newHeight);
            
            // Scale the original image to the new size
            using (SKCanvas canvas = new SKCanvas(resizedImage))
            {
                // Clear the canvas
                canvas.Clear(SKColors.Transparent);
                
                // Draw the image scaled to fit the target dimensions
                using (SKPaint paint = new SKPaint())
                {
                    canvas.DrawBitmap(image, new SKRect(0, 0, image.Width, image.Height), 
                                     new SKRect(0, 0, newWidth, newHeight), paint);
                }
            }
            
            return resizedImage;
        }
        
        // Convenience method for specifically resizing to 640x640
        public static SKBitmap ResizeTo640x640(SKBitmap image)
        {
            return ResizeImage(image, 640, 640);
        }
    }
}