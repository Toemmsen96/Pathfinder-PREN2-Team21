using System.Drawing;
using SkiaSharp;
namespace YoloDetect
{
    public partial class DrawDetected
    {
        private void DrawNonLineDetection(DetectedObject result)
        {
            paint.Color = GetColorForClass(result.ClassName);
            // For non-line objects, show the bounding box and add a label
            SKRect boundingBox = new SKRect(
                result.BoundingBox.TopLeft.X,
                result.BoundingBox.TopLeft.Y,
                result.BoundingBox.BottomRight.X,
                result.BoundingBox.BottomRight.Y
            );


            // Draw just the outline of the bounding box
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 2;
            canvas.DrawRect(boundingBox, paint);

            // Draw the label
            using var textPaint = new SKPaint
            {
                Color = paint.Color,
                IsAntialias = true,
            };

            string shortLabel = result.ClassName + " " + result.Confidence.ToString("F2");
            canvas.DrawText(shortLabel, boundingBox.Left, boundingBox.Top - 5, SKTextAlign.Left, font, textPaint);
            if (result.ClassName.ToLower() == "node")
            {
                // Draw a circle for nodes
                canvas.DrawCircle(result.BoundingBox.GetCenter().X, result.BoundingBox.GetCenter().Y, 5, paint);
            }
            else if (result.ClassName.ToLower() == "pylon" || result.ClassName.ToLower() == "traffic_cone")
            {
                // Draw a circle for pylons
                canvas.DrawCircle(result.BoundingBox.GetLowerCenter().X, result.BoundingBox.GetLowerCenter().Y, 5, paint);
            }

        }
        private void DrawRecheckNodes(DetectedObject recheckNodes)
        {
           paint.Color = SKColors.Red; // Use red for recheck nodes
            // For non-line objects, show the bounding box and add a label
            SKRect boundingBox = new SKRect(
                recheckNodes.BoundingBox.TopLeft.X,
                recheckNodes.BoundingBox.TopLeft.Y,
                recheckNodes.BoundingBox.BottomRight.X,
                recheckNodes.BoundingBox.BottomRight.Y
            );


            // Draw just the outline of the bounding box
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 2;
            canvas.DrawRect(boundingBox, paint);

            // Draw the label
            using var textPaint = new SKPaint
            {
                Color = paint.Color,
                IsAntialias = true,
            };

            string shortLabel = recheckNodes.ClassName + " " + recheckNodes.Confidence.ToString("F2");
            canvas.DrawText(shortLabel, boundingBox.Left, boundingBox.Top - 5, SKTextAlign.Left, font, textPaint);
            if (recheckNodes.ClassName.ToLower() == "node")
            {
                // Draw a circle for nodes
                canvas.DrawCircle(recheckNodes.BoundingBox.GetCenter().X, recheckNodes.BoundingBox.GetCenter().Y, 5, paint);
            }
        }
    }
}