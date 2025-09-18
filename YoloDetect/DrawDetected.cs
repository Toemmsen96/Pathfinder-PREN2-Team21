using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calculations;
using SkiaSharp;
using YoloDotNet.Models;

namespace YoloDetect
{
    public partial class DrawDetected
    {
        private SKCanvas canvas;
        private SKFont font;
        private SKPaint paint;
        private int indicatorSize;
        private bool verboseMode;
        private float nodeProximityThreshold;
        private List<Line> finalLines;
        private List<DetectedObject> recheckNodes = new List<DetectedObject>();
        private List<DetectedObject> drawnNodesAndPylons = new List<DetectedObject>();
        private List<DetectedObject> allResults = new List<DetectedObject>();
        private string imagePath;
        private string outputDir;
        private SKBitmap bitmap; // Store bitmap reference
        private int imageWidth;
        private int imageHeight;

        public DrawDetected(SKCanvas canvas,SKBitmap bitmap, bool verboseMode = false, float nodeProximityThreshold = 15f, string imagePath = "", string outputDir = "")
        {
            this.canvas = canvas;
            this.imageWidth = canvas.DeviceClipBounds.Width;
            this.imageHeight = canvas.DeviceClipBounds.Height;
            Console.WriteLine($"Canvas size: {imageWidth}x{imageHeight}");
            this.verboseMode = verboseMode;
            this.nodeProximityThreshold = nodeProximityThreshold;
            this.finalLines = new List<Line>();
            this.indicatorSize = 8;
            this.imagePath = imagePath;
            this.outputDir = outputDir;
            this.bitmap = bitmap; // Store the bitmap reference
            
            // Initialize default font
            this.font = new SKFont
            {
                Typeface = SKTypeface.FromFamilyName("Arial"),
                Size = 10
            };
            
            // Initialize default paint
            this.paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                StrokeWidth = 2,
                StrokeJoin = SKStrokeJoin.Round,
                Color = SKColors.Green
            };
        }

        public List<Line> GetFinalLines()
        {
            return finalLines;
        }

        public List<DetectedObject> GetMissingNodes()
        {
            return recheckNodes;
        }

        private void DrawFinalLines()
        {
            foreach (var line in finalLines)
            {
                // Draw the line with different colors based on type
                SKColor lineColor = SKColors.Green; // Default color
                if (line.Name.Contains("Recheck"))
                {
                    lineColor = SKColors.HotPink;
                }
                else if (line.Name.Contains("Merged"))
                {
                    lineColor = SKColors.Blue; // Blue for merged lines
                }
                else if (line.Name.Contains("Improved"))
                {
                    lineColor = SKColors.Orange; // Orange for improved lines
                }

                using var linePaint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = line.Name.Contains("Merged") ? 6 : (line.Name.Contains("Improved") ? 5 : 4),
                    Color = lineColor
                };

                canvas.DrawLine(
                    line.Start.X, line.Start.Y,
                    line.End.X, line.End.Y, 
                    linePaint
                );

                // Draw the label for special lines
                if (line.Name.Contains("Recheck") || line.Name.Contains("Merged") || line.Name.Contains("Improved"))
                {
                    using var textPaint = new SKPaint
                    {
                        Color = lineColor,
                        IsAntialias = true
                    };
                    
                    string shortLabel = line.Name.Contains("Merged") ? "Merged" : 
                                       line.Name.Contains("Improved") ? "Improved" : 
                                       "Recheck";
                    canvas.DrawText(shortLabel, line.Start.X + 5, line.Start.Y - 2, SKTextAlign.Left, font, textPaint);
                }
            }
            
            // Debug: Draw connection points if in verbose mode
            if (verboseMode)
            {
                DrawConnectionPointsDebug();
            }
        }

        private void DrawConnectionPointsDebug()
        {
            // This method will use the GetAllConnectionPoints from DrawLine.cs partial class
            var connectionPoints = new List<Coordinate>();
            
            // Add all detected nodes and pylons
            foreach (var obj in drawnNodesAndPylons)
            {
                if (obj.ClassName.Equals("node", StringComparison.OrdinalIgnoreCase))
                {
                    connectionPoints.Add(obj.BoundingBox.GetCenter());
                }
                else if (obj.ClassName.Equals("traffic_cone", StringComparison.OrdinalIgnoreCase) ||
                         obj.ClassName.Equals("pylon", StringComparison.OrdinalIgnoreCase))
                {
                    connectionPoints.Add(obj.BoundingBox.GetLowerCenter());
                }
                else if (IsBarrier(obj.ClassName))
                {
                    connectionPoints.Add(obj.BoundingBox.GetCenter());
                }
            }
            
            using var debugPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = SKColors.Purple
            };
            
            foreach (var point in connectionPoints)
            {
                canvas.DrawCircle(point.X, point.Y, 3, debugPaint);
            }
            
            Console.WriteLine($"Debug: Drew {connectionPoints.Count} connection points in purple");
        }

        public void DrawNodesAndPylons(List<Coordinate> nodesAndPylons)
        {
            foreach (var node in nodesAndPylons)
            {
                // Draw a circle for each node
                canvas.DrawCircle(node.X, node.Y, 15, paint);
            }
        }

        public void DrawDetections(List<DetectedObject> results, List<Coordinate> nodesAndPylons)
        {
            // Store results for later use in merging
            this.allResults = results;
            
            // Set verbose mode for calculation classes
            CalculateDiagonals.SetVerboseMode(verboseMode);
            
            // Draw the bounding boxes for all detections
            foreach (var result in results)
            {
                // Set color based on the class
                paint.Color = GetColorForClass(result.ClassName);

                using var boxPaint = new SKPaint
                {
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = 1,
                    Color = paint.Color
                };

                // Draw the bounding box for all objects
                SKRect boundingBox = new SKRect(
                    result.BoundingBox.TopLeft.X,
                    result.BoundingBox.TopLeft.Y,
                    result.BoundingBox.BottomRight.X,
                    result.BoundingBox.BottomRight.Y
                );

                //canvas.DrawRect(boundingBox, boxPaint);

                if (IsLine(result.ClassName))
                {
                    DrawLineDetection(result, boxPaint, nodesAndPylons);
                }
                else
                {
                    DrawNonLineDetection(result);
                    drawnNodesAndPylons.Add(result);
                }
            }
            
            

            // Draw any lines that need to be rechecked
            DrawRecheckLines(imageHeight, imageWidth);

            // Count nodes and pylons (pylons represent nodes beneath them)
            int nodeAndPylonCount = 0;
            var nodeTypes = new List<string>();
            var otherTypes = new List<string>();
            
            foreach (var obj in drawnNodesAndPylons)
            {
                if (obj.ClassName.Equals("node", StringComparison.OrdinalIgnoreCase) ||
                    obj.ClassName.Equals("pylon", StringComparison.OrdinalIgnoreCase) ||
                    obj.ClassName.Equals("pylons", StringComparison.OrdinalIgnoreCase) ||
                    obj.ClassName.Equals("traffic_cone", StringComparison.OrdinalIgnoreCase))
                {
                    nodeAndPylonCount++;
                    nodeTypes.Add(obj.ClassName);
                }
                else
                {
                    otherTypes.Add(obj.ClassName);
                }
            }
            
            Console.WriteLine($"Total detected objects: {drawnNodesAndPylons.Count}");
            Console.WriteLine($"Nodes and pylons count: {nodeAndPylonCount} ({string.Join(", ", nodeTypes)})");
            if (otherTypes.Count > 0)
            {
                Console.WriteLine($"Other objects: {otherTypes.Count} ({string.Join(", ", otherTypes)})");
            }
            
            // Calculate missing nodes if we have fewer than the expected 8 nodes/pylons
            // We expect 8 total, so calculate missing nodes for any count less than 8
            if (nodeAndPylonCount < 8)
            {
                int expectedMissing = 8 - nodeAndPylonCount;
                Console.WriteLine($"Node/pylon count ({nodeAndPylonCount}) is less than expected 8. Expected missing: {expectedMissing}");
                CalculateMissingNodes calculateMissingNodes = new CalculateMissingNodes(nodesAndPylons, nodeProximityThreshold);
                recheckNodes = calculateMissingNodes.GetMissingNodes(drawnNodesAndPylons, finalLines);
            }
            else if (nodeAndPylonCount == 8)
            {
                Console.WriteLine($"Node/pylon count ({nodeAndPylonCount}) matches expected 8, no missing nodes to calculate.");
            }
            else
            {
                Console.WriteLine($"Node/pylon count ({nodeAndPylonCount}) exceeds expected 8, skipping missing node calculation.");
            }

            // Merge lines that meet at barriers - do this after all lines are finalized
            List<DetectedObject> barriers = GetBarriers(allResults);
            
            // Debug: Print all current lines before merging
            Console.WriteLine("=== Lines before merging ===");
            for (int i = 0; i < finalLines.Count; i++)
            {
                var line = finalLines[i];
                Console.WriteLine($"Line {i}: {line.Name} - ({line.Start.X:F1},{line.Start.Y:F1}) -> ({line.End.X:F1},{line.End.Y:F1})");
            }
            Console.WriteLine("=== Barriers found ===");
            for (int i = 0; i < barriers.Count; i++)
            {
                var barrier = barriers[i];
                var center = barrier.BoundingBox.GetCenter();
                Console.WriteLine($"Barrier {i}: {barrier.ClassName} at ({center.X:F1},{center.Y:F1})");
            }
            
            MergeLinesAtBarriers(barriers);

            
            foreach (DetectedObject recheckNode in recheckNodes)
            {
                DrawRecheckNodes(recheckNode);
            }

            // Draw all the final lines at the end
            DrawFinalLines();


            // Save the image if paths are provided
            if (!string.IsNullOrEmpty(imagePath) && !string.IsNullOrEmpty(outputDir))
            {
                SaveImage(imagePath, outputDir);
            }
            else
            {
                Console.WriteLine("Image path or output directory not provided, skipping save.");
            }
        }

        public void SaveImage(string imagePath, string outputDir)
        {
            try
            {
                // Make sure output directory exists
                if (!Directory.Exists(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
                
                // Create output filename
                string outputFileName = Path.GetFileNameWithoutExtension(imagePath) + "_detected.jpg";
                string outputPath = Path.Combine(outputDir, outputFileName);
                
                // Use the bitmap if we have it
                if (bitmap != null)
                {
                    using var image = SKImage.FromBitmap(bitmap);
                    using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                    using var stream = File.OpenWrite(outputPath);
                    data.SaveTo(stream);
                    
                    Console.WriteLine($"Results saved to: {outputPath}");
                }
                else
                {
                    Console.WriteLine("Warning: No bitmap available to save results.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving image: {ex.Message}");
            }
        }

        private static bool IsLine(string label)
        {
            return label.Equals("line", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBarrier(string label)
        {
            return label.Equals("barrier", StringComparison.OrdinalIgnoreCase) ||
                   label.Equals("barrier_white", StringComparison.OrdinalIgnoreCase) ||
                   label.Equals("barrier_red", StringComparison.OrdinalIgnoreCase);
        }

        private static List<DetectedObject> GetBarriers(List<DetectedObject> results)
        {
            return results.Where(r => IsBarrier(r.ClassName)).ToList();
        }

        private void MergeLinesAtBarriers(List<DetectedObject> barriers)
        {
            if (finalLines.Count < 2) return;

            Console.WriteLine($"Checking for line merging at {barriers.Count} barriers...");
            Console.WriteLine($"Total lines available for merging: {finalLines.Count}");

            List<Line> mergedLines = new List<Line>();
            List<Line> processedLines = new List<Line>();

            foreach (var barrier in barriers)
            {
                var barrierCenter = barrier.BoundingBox.GetCenter();
                List<Line> linesAtBarrier = new List<Line>();

                Console.WriteLine($"Checking barrier {barrier.ClassName} at ({barrierCenter.X:F1}, {barrierCenter.Y:F1})");

                // Find lines that have any endpoint near this barrier - use a more generous threshold
                float mergeThreshold = nodeProximityThreshold * 25f; // Use double the normal threshold for merging
                
                foreach (var line in finalLines)
                {
                    if (processedLines.Contains(line)) continue;

                    float distanceToStart = Coordinate.Distance(line.Start, barrierCenter);
                    float distanceToEnd = Coordinate.Distance(line.End, barrierCenter);
                    
                    // Check if either end of the line is close to the barrier
                    if (distanceToStart < mergeThreshold || distanceToEnd < mergeThreshold)
                    {
                        linesAtBarrier.Add(line);
                        Console.WriteLine($"  Line {line.Name} is close to barrier (distances: start={distanceToStart:F1}, end={distanceToEnd:F1}, threshold={mergeThreshold:F1})");
                    }
                }

                Console.WriteLine($"Found {linesAtBarrier.Count} lines near barrier {barrier.ClassName}");

                // Try to merge lines - handle cases with 2 or more lines
                if (linesAtBarrier.Count >= 2)
                {
                    // For simplicity, merge the first two lines that are closest to the barrier
                    var sortedLines = linesAtBarrier
                        .Select(line => new
                        {
                            Line = line,
                            MinDistance = Math.Min(
                                Coordinate.Distance(line.Start, barrierCenter),
                                Coordinate.Distance(line.End, barrierCenter)
                            )
                        })
                        .OrderBy(x => x.MinDistance)
                        .Take(2)
                        .ToList();

                    if (sortedLines.Count == 2)
                    {
                        var line1 = sortedLines[0].Line;
                        var line2 = sortedLines[1].Line;

                        // Determine the best connection points
                        var connectionPoints = GetBestConnectionPoints(line1, line2, barrierCenter);
                        
                        if (connectionPoints != null)
                        {
                            // Create a merged line
                            var mergedLine = new Line($"Merged_{GetShortName(line1.Name)}_{GetShortName(line2.Name)}", 
                                                    connectionPoints.Item1, connectionPoints.Item2);
                            mergedLines.Add(mergedLine);

                            // Mark these lines as processed
                            processedLines.Add(line1);
                            processedLines.Add(line2);

                            Console.WriteLine($"✓ Merged lines {line1.Name} and {line2.Name} at barrier {barrier.ClassName}");
                            Console.WriteLine($"  Merged line: ({connectionPoints.Item1.X:F1},{connectionPoints.Item1.Y:F1}) -> ({connectionPoints.Item2.X:F1},{connectionPoints.Item2.Y:F1})");
                        }
                    }
                }
                else if (linesAtBarrier.Count > 2)
                {
                    Console.WriteLine($"⚠ Found {linesAtBarrier.Count} lines at barrier {barrier.ClassName} - merging closest pair");
                }
            }

            // Replace the original lines with merged lines
            if (mergedLines.Count > 0)
            {
                // Remove processed lines from finalLines
                foreach (var processedLine in processedLines)
                {
                    finalLines.Remove(processedLine);
                }

                // Add merged lines
                finalLines.AddRange(mergedLines);

                Console.WriteLine($"✓ Successfully merged {processedLines.Count} lines into {mergedLines.Count} merged lines");
            }
            else
            {
                Console.WriteLine("No lines were merged at barriers");
            }
        }

        private Coordinate GetClosestEndToPoint(Line line, Coordinate point)
        {
            float distanceToStart = Coordinate.Distance(line.Start, point);
            float distanceToEnd = Coordinate.Distance(line.End, point);
            
            return distanceToStart < distanceToEnd ? line.Start : line.End;
        }

        private string GetShortName(string name)
        {
            // Extract just the GUID part or a shortened version for cleaner naming
            if (name.Length > 8)
            {
                return name.Substring(0, 8);
            }
            return name;
        }

        private Tuple<Coordinate, Coordinate> GetBestConnectionPoints(Line line1, Line line2, Coordinate barrierCenter)
        {
            // Calculate all possible connections and find the one that makes most sense
            // The goal is to connect the ends that are farthest from the barrier (to create a continuous line through the barrier)
            
            var endpoints = new[]
            {
                new { Point = line1.Start, Line = 1, IsStart = true, DistanceToBarrier = Coordinate.Distance(line1.Start, barrierCenter) },
                new { Point = line1.End, Line = 1, IsStart = false, DistanceToBarrier = Coordinate.Distance(line1.End, barrierCenter) },
                new { Point = line2.Start, Line = 2, IsStart = true, DistanceToBarrier = Coordinate.Distance(line2.Start, barrierCenter) },
                new { Point = line2.End, Line = 2, IsStart = false, DistanceToBarrier = Coordinate.Distance(line2.End, barrierCenter) }
            };

            // Find the endpoints that are farthest from the barrier (one from each line)
            var line1FarthestPoint = endpoints.Where(e => e.Line == 1).OrderByDescending(e => e.DistanceToBarrier).First();
            var line2FarthestPoint = endpoints.Where(e => e.Line == 2).OrderByDescending(e => e.DistanceToBarrier).First();

            Console.WriteLine($"  Line1 farthest point: ({line1FarthestPoint.Point.X:F1},{line1FarthestPoint.Point.Y:F1}) distance={line1FarthestPoint.DistanceToBarrier:F1}");
            Console.WriteLine($"  Line2 farthest point: ({line2FarthestPoint.Point.X:F1},{line2FarthestPoint.Point.Y:F1}) distance={line2FarthestPoint.DistanceToBarrier:F1}");

            return new Tuple<Coordinate, Coordinate>(line1FarthestPoint.Point, line2FarthestPoint.Point);
        }

        // Example of how to use this class to draw everything
        public static void DrawData(SKBitmap bitmap, List<DetectedObject> results, bool verboseMode = false, 
                               float nodeProximityThreshold = 30f, string imagePath = "", string outputDir = "")
        {
            using var canvas = new SKCanvas(bitmap);
            var drawer = new DrawDetected(canvas,bitmap, verboseMode, nodeProximityThreshold, imagePath, outputDir);

            // Get nodes and pylons
            List<Coordinate> nodesAndPylons = GetNodesAndPylons(results);

            // Draw nodes and pylons
            drawer.DrawNodesAndPylons(nodesAndPylons);

            // Draw all detections
            drawer.DrawDetections(results, nodesAndPylons);
        }

        // Helper method to extract nodes and pylons from detections
        private static List<Coordinate> GetNodesAndPylons(List<DetectedObject> results)
        {
            List<Coordinate> nodesAndPylons = new List<Coordinate>();
            
            foreach (var result in results)
            {
                if (result.ClassName.Equals("node", StringComparison.OrdinalIgnoreCase) )
                {
                    nodesAndPylons.Add(result.BoundingBox.GetCenter());
                }
                else if (result.ClassName.Equals("traffic_cone", StringComparison.OrdinalIgnoreCase) ||
                    result.ClassName.Equals("pylon", StringComparison.OrdinalIgnoreCase))
                {
                    nodesAndPylons.Add(result.BoundingBox.GetLowerCenter());
                }
                else if (IsBarrier(result.ClassName))
                {
                    // Include barriers as potential connection points
                    nodesAndPylons.Add(result.BoundingBox.GetCenter());
                }
            }
            
            return nodesAndPylons;
        }
    }
}