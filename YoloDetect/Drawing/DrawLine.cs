using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;


namespace YoloDetect
{
    public partial class DrawDetected{
        private void DrawLineDetection(DetectedObject result, SKPaint boxPaint, List<Coordinate> nodesAndPylons)
        {
            // For line detections, draw diagonal lines
            SKRect boundingBox = new SKRect(
                result.BoundingBox.TopLeft.X,
                result.BoundingBox.TopLeft.Y,
                result.BoundingBox.BottomRight.X,
                result.BoundingBox.BottomRight.Y
            );

            Line? diagonal = null;
            
            // Try enhanced calculation if we have enough connection points
            if (nodesAndPylons.Count >= 2)
            {
                diagonal = CalculateDiagonals.CalculateOptimalDiagonal(boundingBox, nodesAndPylons, imageHeight, imageWidth, nodeProximityThreshold);
                if (diagonal != null)
                {
                    Console.WriteLine($"Using enhanced diagonal calculation for line detection");
                }
            }
            
            // Fallback to original method if enhanced calculation didn't work
            if (diagonal == null)
            {
                diagonal = CalculateDiagonals.CalculateDiagonal(boundingBox, nodesAndPylons, imageHeight, imageWidth, nodeProximityThreshold);
            }
            
            if (diagonal == null)
            {
                Console.WriteLine("No diagonal found, drawing X pattern for line detection.");
                canvas.DrawLine(
                    boundingBox.Left, boundingBox.Top, 
                    boundingBox.Right, boundingBox.Bottom, 
                    boxPaint
                );
            
                canvas.DrawLine(
                    boundingBox.Right, boundingBox.Top, 
                    boundingBox.Left, boundingBox.Bottom, 
                    boxPaint
                );
                return;
            }

            // Add the diagonal to finalLines instead of drawing immediately
            finalLines.Add(diagonal);
            
            // Draw small filled indicator at bottom right of the detection box
            float indicatorX = boundingBox.Right - indicatorSize;
            float indicatorY = boundingBox.Bottom - indicatorSize;
            //canvas.DrawRect(indicatorX, indicatorY, indicatorSize, indicatorSize, paint);
            
            // Draw the label
            using var textPaint = new SKPaint
            {
                Color = paint.Color,
                IsAntialias = true
            };
            
            string shortLabel = result.ClassName + " " + result.Confidence.ToString("F2");
            //canvas.DrawText(shortLabel, indicatorX - 5, indicatorY - 2, SKTextAlign.Left, font, textPaint);
        }


        public void DrawRecheckLines(int imageHeight, int imageWidth)
        {
            foreach (var line in CalculateDiagonals.GetLinesToRecheck())
            {
                if (verboseMode) Console.WriteLine($"Line to recheck: {line}");
                Line? diagonal = CalculateDiagonals.CalculateDiagonalOnLineEnd(line, finalLines,imageHeight,imageWidth, nodeProximityThreshold);
                if (diagonal != null)
                {
                    // Set the name to indicate it's a recheck line
                    diagonal.Name = "Recheck " + diagonal.Name;
                    finalLines.Add(diagonal);
                    Console.WriteLine($"Diagonal found: {diagonal.Name}");
                }
            }
            
            // After initial recheck, perform iterative improvements
            PerformIterativeLineRecalculation(imageHeight, imageWidth);
        }

        private void PerformIterativeLineRecalculation(int imageHeight, int imageWidth)
        {
            Console.WriteLine("=== Starting iterative line recalculation ===");
            
            bool linesImproved = true;
            int iteration = 0;
            int maxIterations = 3; // Prevent infinite loops
            
            while (linesImproved && iteration < maxIterations)
            {
                iteration++;
                linesImproved = false;
                Console.WriteLine($"Iteration {iteration}: Checking {finalLines.Count} lines for improvements");
                
                List<Line> improvedLines = new List<Line>();
                List<Line> linesToRemove = new List<Line>();
                
                // Get all potential connection points (nodes, pylons, line endpoints)
                List<Coordinate> allConnectionPoints = GetAllConnectionPoints();
                
                foreach (var line in finalLines.ToList()) // ToList to avoid modification during iteration
                {
                    // Check if this line can be improved by connecting to better endpoints
                    var improvedLine = TryImproveLineConnection(line, allConnectionPoints, imageHeight, imageWidth);
                    
                    if (improvedLine != null && IsLineImprovement(line, improvedLine, allConnectionPoints))
                    {
                        improvedLines.Add(improvedLine);
                        linesToRemove.Add(line);
                        linesImproved = true;
                        Console.WriteLine($"  Improved line {line.Name} -> {improvedLine.Name}");
                    }
                }
                
                // Apply improvements
                foreach (var lineToRemove in linesToRemove)
                {
                    finalLines.Remove(lineToRemove);
                }
                finalLines.AddRange(improvedLines);
                
                Console.WriteLine($"Iteration {iteration} complete: {improvedLines.Count} lines improved");
            }
            
            Console.WriteLine($"=== Iterative recalculation complete after {iteration} iterations ===");
        }

        private List<Coordinate> GetAllConnectionPoints()
        {
            List<Coordinate> connectionPoints = new List<Coordinate>();
            
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
            
            // Add endpoints of existing lines (for line-to-line connections)
            foreach (var line in finalLines)
            {
                connectionPoints.Add(line.Start);
                connectionPoints.Add(line.End);
            }
            
            return connectionPoints;
        }

        private Line? TryImproveLineConnection(Line originalLine, List<Coordinate> connectionPoints, int imageHeight, int imageWidth)
        {
            // Calculate threshold for connection proximity
            float connectionThreshold = nodeProximityThreshold * 1.5f;
            
            // Find potential connection points near the original line's endpoints
            var startNearbyPoints = connectionPoints
                .Where(p => Coordinate.Distance(p, originalLine.Start) < connectionThreshold && 
                           !IsSamePoint(p, originalLine.Start))
                .OrderBy(p => Coordinate.Distance(p, originalLine.Start))
                .ToList();
                
            var endNearbyPoints = connectionPoints
                .Where(p => Coordinate.Distance(p, originalLine.End) < connectionThreshold && 
                           !IsSamePoint(p, originalLine.End))
                .OrderBy(p => Coordinate.Distance(p, originalLine.End))
                .ToList();
            
            if (verboseMode)
            {
                Console.WriteLine($"    Line {originalLine.Name}: {startNearbyPoints.Count} points near start, {endNearbyPoints.Count} points near end");
            }
            
            // Try different connection combinations
            Line? bestImprovement = null;
            float bestScore = CalculateLineScore(originalLine, connectionPoints);
            
            // Try snapping start point to nearby connections
            foreach (var newStart in startNearbyPoints.Take(3)) // Limit to top 3 closest
            {
                var candidateLine = new Line($"Improved_{originalLine.Name}_Start", newStart, originalLine.End);
                float score = CalculateLineScore(candidateLine, connectionPoints);
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestImprovement = candidateLine;
                }
            }
            
            // Try snapping end point to nearby connections
            foreach (var newEnd in endNearbyPoints.Take(3)) // Limit to top 3 closest
            {
                var candidateLine = new Line($"Improved_{originalLine.Name}_End", originalLine.Start, newEnd);
                float score = CalculateLineScore(candidateLine, connectionPoints);
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestImprovement = candidateLine;
                }
            }
            
            // Try snapping both endpoints
            foreach (var newStart in startNearbyPoints.Take(2))
            {
                foreach (var newEnd in endNearbyPoints.Take(2))
                {
                    var candidateLine = new Line($"Improved_{originalLine.Name}_Both", newStart, newEnd);
                    float score = CalculateLineScore(candidateLine, connectionPoints);
                    
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestImprovement = candidateLine;
                    }
                }
            }
            
            return bestImprovement;
        }

        private float CalculateLineScore(Line line, List<Coordinate> connectionPoints)
        {
            float score = 0f;
            
            // Score based on how many connection points are near the line endpoints
            float endpointBonus = 10f;
            float proximityThreshold = nodeProximityThreshold;
            
            foreach (var point in connectionPoints)
            {
                float distanceToStart = Coordinate.Distance(point, line.Start);
                float distanceToEnd = Coordinate.Distance(point, line.End);
                
                // Bonus for endpoints that are very close to connection points
                if (distanceToStart < proximityThreshold)
                {
                    score += endpointBonus * (1f - (distanceToStart / proximityThreshold));
                }
                if (distanceToEnd < proximityThreshold)
                {
                    score += endpointBonus * (1f - (distanceToEnd / proximityThreshold));
                }
            }
            
            // Penalty for very short lines (might be noise)
            float length = line.Length();
            if (length < nodeProximityThreshold)
            {
                score -= 5f;
            }
            
            return score;
        }

        private bool IsLineImprovement(Line originalLine, Line improvedLine, List<Coordinate> connectionPoints)
        {
            // An improved line should have significantly better connections
            float originalScore = CalculateLineScore(originalLine, connectionPoints);
            float improvedScore = CalculateLineScore(improvedLine, connectionPoints);
            
            // Require at least 20% improvement to avoid minor fluctuations
            return improvedScore > originalScore * 1.2f;
        }

        private bool IsSamePoint(Coordinate p1, Coordinate p2, float tolerance = 1f)
        {
            return Coordinate.Distance(p1, p2) < tolerance;
        }
    }
}