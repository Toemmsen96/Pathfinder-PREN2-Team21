using SkiaSharp;

namespace YoloDetect
{
    public partial class DrawDetected{
        public static SKColor GetColorForClass(string className)
        {
            // You can define specific colors for specific labels
            switch (className.ToLower())
            {
                case "line":
                    return new SKColor(255, 0, 0);  // Red for line
                case "pylon":
                case "pylons":
                case "traffic_cone":
                    return new SKColor(0, 0, 255);  // Blue for pylon
                case "node":
                    return new SKColor(0, 255, 0);  // Green for node
                case "barrier":
                case "barrier_white":
                    return new SKColor(255, 255, 0);  // Yellow for Barrier
                case "barrier_red":
                    return new SKColor(255, 0, 255);  // Magenta for Barrier Red
                default:
                    // Generate a color based on the hash of the label string
                    int hash = className.GetHashCode();
                    return new SKColor(
                        (byte)((hash & 0xFF0000) >> 16),
                        (byte)((hash & 0x00FF00) >> 8),
                        (byte)(hash & 0x0000FF)
                    );
            }
        }
    }
}