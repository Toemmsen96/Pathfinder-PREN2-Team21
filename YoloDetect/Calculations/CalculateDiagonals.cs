using System;
using System.Collections.Generic;
using SkiaSharp;
using System.Linq;

namespace YoloDetect
{
    public class CalculateDiagonals
    {
        private static List<SKRect> linesToRecheck = new List<SKRect>();
        private static bool verboseMode = false;

        public static void SetVerboseMode(bool verbose)
        {
            verboseMode = verbose;
        }

        public static List<SKRect> GetLinesToRecheck()
        {
            return linesToRecheck;
        }

        public static void ClearLinesToRecheck()
        {
            Console.WriteLine("Clearing lines to recheck.");
            linesToRecheck.Clear();
            Console.WriteLine("Lines to recheck cleared.");
        }
        
        // Calculate threshold in pixels based on percentage of image dimensions
        private static float CalculateThreshold(int imageHeight, int imageWidth, float thresholdPercentage)
        {
            float widthThreshold = imageWidth * thresholdPercentage / 100f;
            float heightThreshold = imageHeight * thresholdPercentage / 100f;
            return Math.Max(widthThreshold, heightThreshold);
        }
        
        public static Line? CalculateDiagonal(SKRect rect, List<Coordinate> nodesAndPylons, int imageHeight, int imageWidth, float thresholdPercentage = 2f)
        {
            // Calculate threshold in pixels based on percentage
            float threshold = CalculateThreshold(imageHeight, imageWidth, thresholdPercentage);
            
            Console.WriteLine($"Using threshold: {threshold}px (based on {thresholdPercentage}% of image dimensions)");
            
            // First try with normal threshold
            var result = TryCalculateDiagonal(rect, nodesAndPylons, threshold, "normal");
            if (result != null) return result;
            
            // If no diagonal found, try with a slightly larger threshold (but still reasonable)
            float relaxedThreshold = threshold * 1.3f;
            Console.WriteLine($"No diagonal found with normal threshold, trying relaxed threshold: {relaxedThreshold}px");
            result = TryCalculateDiagonal(rect, nodesAndPylons, relaxedThreshold, "relaxed");
            if (result != null) return result;
            
            // Still no luck, add to recheck list
            linesToRecheck.Add(rect);
            Console.WriteLine("No diagonal found even with relaxed threshold, adding to recheck list.");
            return null;
        }

        private static Line? TryCalculateDiagonal(SKRect rect, List<Coordinate> nodesAndPylons, float threshold, string mode)
        {
            // Create the two possible diagonals
            var diagonal1 = new Line(
                new Coordinate(rect.Left, rect.Top),     // Top-left
                new Coordinate(rect.Right, rect.Bottom)  // Bottom-right
            );
             var diagonal2 = new Line(
                new Coordinate(rect.Right, rect.Top),    // Top-right
                new Coordinate(rect.Left, rect.Bottom)   // Bottom-left
            );

            // Count nodes close to each diagonal and calculate average distances
            int diagonal1Proximity = 0;
            int diagonal2Proximity = 0;
            float diagonal1TotalDistance = 0f;
            float diagonal2TotalDistance = 0f;
            
            foreach (var node in nodesAndPylons)
            {
                // Calculate the distance from the node to each diagonal line
                float distanceToDiagonal1 = diagonal1.DistancePointToLine(node);
                if (verboseMode) Console.WriteLine($"Distance to diagonal 1: {distanceToDiagonal1}");
                if (distanceToDiagonal1 < threshold)
                {
                    diagonal1Proximity++;
                    diagonal1TotalDistance += distanceToDiagonal1;
                    Console.WriteLine($"Node at ({node.X}, {node.Y}) is close to diagonal 1, distance: {distanceToDiagonal1}");
                }
                
                float distanceToDiagonal2 = diagonal2.DistancePointToLine(node);
                if (verboseMode) Console.WriteLine($"Distance to diagonal 2: {distanceToDiagonal2}");
                if (distanceToDiagonal2 < threshold)
                {
                    diagonal2Proximity++;
                    diagonal2TotalDistance += distanceToDiagonal2;
                    Console.WriteLine($"Node at ({node.X}, {node.Y}) is close to diagonal 2, distance: {distanceToDiagonal2}");
                }
            }
            
            // Calculate average distances for comparison
            float diagonal1AvgDistance = diagonal1Proximity > 0 ? diagonal1TotalDistance / diagonal1Proximity : float.MaxValue;
            float diagonal2AvgDistance = diagonal2Proximity > 0 ? diagonal2TotalDistance / diagonal2Proximity : float.MaxValue;
            
            // Check if endpoints connect to nodes for each diagonal
            bool diagonal1StartConnected = CheckEndpointConnection(diagonal1.Start, nodesAndPylons, threshold);
            bool diagonal1EndConnected = CheckEndpointConnection(diagonal1.End, nodesAndPylons, threshold);
            bool diagonal2StartConnected = CheckEndpointConnection(diagonal2.Start, nodesAndPylons, threshold);
            bool diagonal2EndConnected = CheckEndpointConnection(diagonal2.End, nodesAndPylons, threshold);
            
            bool diagonal1Connected = diagonal1StartConnected && diagonal1EndConnected;
            bool diagonal2Connected = diagonal2StartConnected && diagonal2EndConnected;
            
            Console.WriteLine($"[{mode}] Diagonal 1 (TL-BR) has {diagonal1Proximity} close nodes, avg distance: {diagonal1AvgDistance:F2}, endpoints connected: {diagonal1Connected}");
            Console.WriteLine($"[{mode}] Diagonal 2 (TR-BL) has {diagonal2Proximity} close nodes, avg distance: {diagonal2AvgDistance:F2}, endpoints connected: {diagonal2Connected}");

            if (diagonal1Proximity == 0 && diagonal2Proximity == 0)
            {
                Console.WriteLine($"[{mode}] No nodes close to either diagonal.");
                return null; // Return null if no proximity
            }
            
            // Prioritize diagonals with properly connected endpoints
            if (diagonal1Connected && !diagonal2Connected)
            {
                Console.WriteLine($"[{mode}] Selecting diagonal 1 (endpoints properly connected)");
                return diagonal1;
            }
            else if (diagonal2Connected && !diagonal1Connected)
            {
                Console.WriteLine($"[{mode}] Selecting diagonal 2 (endpoints properly connected)");
                return diagonal2;
            }
            else if (!diagonal1Connected && !diagonal2Connected)
            {
                Console.WriteLine($"[{mode}] Neither diagonal has properly connected endpoints, falling back to proximity logic");
                // Continue with original logic below
            }
            
            // Enhanced selection logic: prefer more nearby nodes, but if counts are similar, prefer closer average distance
            if (diagonal1Proximity > diagonal2Proximity)
            {
                Console.WriteLine($"[{mode}] Selecting diagonal 1 (more nearby nodes)");
                return diagonal1;
            }
            else if (diagonal2Proximity > diagonal1Proximity)
            {
                Console.WriteLine($"[{mode}] Selecting diagonal 2 (more nearby nodes)");
                return diagonal2;
            }
            else
            {
                // Same number of nearby nodes - choose based on average distance
                if (diagonal1AvgDistance <= diagonal2AvgDistance)
                {
                    Console.WriteLine($"[{mode}] Selecting diagonal 1 (same node count, better avg distance: {diagonal1AvgDistance:F2} vs {diagonal2AvgDistance:F2})");
                    return diagonal1;
                }
                else
                {
                    Console.WriteLine($"[{mode}] Selecting diagonal 2 (same node count, better avg distance: {diagonal2AvgDistance:F2} vs {diagonal1AvgDistance:F2})");
                    return diagonal2;
                }
            }
        }

        public static Line? CalculateDiagonalOnLineEnd(SKRect rect, List<Line> lines, int imageHeight, int imageWidth, float thresholdPercentage = 2f)
        {
            // Calculate threshold in pixels based on percentage
            float threshold = CalculateThreshold(imageHeight, imageWidth, thresholdPercentage);
            
            if (verboseMode) Console.WriteLine($"Using threshold: {threshold}px (based on {thresholdPercentage}% of image dimensions)");
            
            // Create the two possible diagonals
            var diagonal1 = new Line(
                new Coordinate(rect.Left, rect.Top),     // Top-left
                new Coordinate(rect.Right, rect.Bottom)  // Bottom-right
            );
            
            var diagonal2 = new Line(
                new Coordinate(rect.Right, rect.Top),    // Top-right
                new Coordinate(rect.Left, rect.Bottom)   // Bottom-left
            );
            
            // Count lines close to each diagonal and calculate average distances
            int diagonal1Proximity = 0;
            int diagonal2Proximity = 0;
            float diagonal1TotalDistance = 0f;
            float diagonal2TotalDistance = 0f;
            
            foreach (var line in lines)
            {
                // Check both endpoints of each line for proximity to diagonals
                float distanceStartToDiagonal1 = diagonal1.DistancePointToLine(line.Start);
                float distanceEndToDiagonal1 = diagonal1.DistancePointToLine(line.End);
                float distanceStartToDiagonal2 = diagonal2.DistancePointToLine(line.Start);
                float distanceEndToDiagonal2 = diagonal2.DistancePointToLine(line.End);
                
                if (verboseMode)
                {
                    Console.WriteLine($"Line {line.Name}: D1 distances start={distanceStartToDiagonal1:F1}, end={distanceEndToDiagonal1:F1}");
                    Console.WriteLine($"Line {line.Name}: D2 distances start={distanceStartToDiagonal2:F1}, end={distanceEndToDiagonal2:F1}");
                }
                
                // For diagonal 1, use the minimum distance (closest endpoint)
                float minDistanceToDiagonal1 = Math.Min(distanceStartToDiagonal1, distanceEndToDiagonal1);
                if (minDistanceToDiagonal1 < threshold)
                {
                    diagonal1Proximity++;
                    diagonal1TotalDistance += minDistanceToDiagonal1;
                    Console.WriteLine($"Line {line.Name} is close to diagonal 1, min distance: {minDistanceToDiagonal1:F1}");
                }
                
                // For diagonal 2, use the minimum distance (closest endpoint)
                float minDistanceToDiagonal2 = Math.Min(distanceStartToDiagonal2, distanceEndToDiagonal2);
                if (minDistanceToDiagonal2 < threshold)
                {
                    diagonal2Proximity++;
                    diagonal2TotalDistance += minDistanceToDiagonal2;
                    Console.WriteLine($"Line {line.Name} is close to diagonal 2, min distance: {minDistanceToDiagonal2:F1}");
                }
            }
            
            // Calculate average distances for comparison
            float diagonal1AvgDistance = diagonal1Proximity > 0 ? diagonal1TotalDistance / diagonal1Proximity : float.MaxValue;
            float diagonal2AvgDistance = diagonal2Proximity > 0 ? diagonal2TotalDistance / diagonal2Proximity : float.MaxValue;
            
            // Get all line endpoints for connection checking
            var lineEndpoints = new List<Coordinate>();
            foreach (var line in lines)
            {
                lineEndpoints.Add(line.Start);
                lineEndpoints.Add(line.End);
            }
            
            // Check if endpoints connect to line endpoints for each diagonal
            bool diagonal1StartConnected = CheckEndpointConnection(diagonal1.Start, lineEndpoints, threshold);
            bool diagonal1EndConnected = CheckEndpointConnection(diagonal1.End, lineEndpoints, threshold);
            bool diagonal2StartConnected = CheckEndpointConnection(diagonal2.Start, lineEndpoints, threshold);
            bool diagonal2EndConnected = CheckEndpointConnection(diagonal2.End, lineEndpoints, threshold);
            
            bool diagonal1Connected = diagonal1StartConnected && diagonal1EndConnected;
            bool diagonal2Connected = diagonal2StartConnected && diagonal2EndConnected;
            
            Console.WriteLine($"Diagonal 1 (TL-BR) has {diagonal1Proximity} close lines, avg distance: {diagonal1AvgDistance:F2}, endpoints connected: {diagonal1Connected}");
            Console.WriteLine($"Diagonal 2 (TR-BL) has {diagonal2Proximity} close lines, avg distance: {diagonal2AvgDistance:F2}, endpoints connected: {diagonal2Connected}");

            if (diagonal1Proximity == 0 && diagonal2Proximity == 0)
            {
                Console.WriteLine("No lines close to either diagonal.");
                return null; // Return null if no proximity
            }
            
            // Prioritize diagonals with properly connected endpoints
            if (diagonal1Connected && !diagonal2Connected)
            {
                Console.WriteLine("Selecting diagonal 1 (endpoints properly connected to lines)");
                return diagonal1;
            }
            else if (diagonal2Connected && !diagonal1Connected)
            {
                Console.WriteLine("Selecting diagonal 2 (endpoints properly connected to lines)");
                return diagonal2;
            }
            else if (!diagonal1Connected && !diagonal2Connected)
            {
                Console.WriteLine("Neither diagonal has properly connected endpoints, falling back to proximity logic");
                // Continue with original logic below
            }
            
            // Enhanced selection logic: prefer more nearby lines, but if counts are similar, prefer closer average distance
            if (diagonal1Proximity > diagonal2Proximity)
            {
                Console.WriteLine("Selecting diagonal 1 (more nearby lines)");
                return diagonal1;
            }
            else if (diagonal2Proximity > diagonal1Proximity)
            {
                Console.WriteLine("Selecting diagonal 2 (more nearby lines)");
                return diagonal2;
            }
            else
            {
                // Same number of nearby lines - choose based on average distance
                if (diagonal1AvgDistance <= diagonal2AvgDistance)
                {
                    Console.WriteLine($"Selecting diagonal 1 (same line count, better avg distance: {diagonal1AvgDistance:F2} vs {diagonal2AvgDistance:F2})");
                    return diagonal1;
                }
                else
                {
                    Console.WriteLine($"Selecting diagonal 2 (same line count, better avg distance: {diagonal2AvgDistance:F2} vs {diagonal1AvgDistance:F2})");
                    return diagonal2;
                }
            }
        }

        // New method for enhanced line calculation considering all connection points
        public static Line? CalculateOptimalDiagonal(SKRect rect, List<Coordinate> connectionPoints, int imageHeight, int imageWidth, float thresholdPercentage = 2f)
        {
            float threshold = CalculateThreshold(imageHeight, imageWidth, thresholdPercentage);
            
            if (verboseMode) Console.WriteLine($"Calculating optimal diagonal with {connectionPoints.Count} connection points, threshold: {threshold}px");
            
            // Create the two possible diagonals
            var diagonal1 = new Line(
                new Coordinate(rect.Left, rect.Top),     // Top-left
                new Coordinate(rect.Right, rect.Bottom)  // Bottom-right
            );
            
            var diagonal2 = new Line(
                new Coordinate(rect.Right, rect.Top),    // Top-right
                new Coordinate(rect.Left, rect.Bottom)   // Bottom-left
            );
            
            // Score each diagonal based on proximity to connection points
            float diagonal1Score = CalculateDiagonalScore(diagonal1, connectionPoints, threshold);
            float diagonal2Score = CalculateDiagonalScore(diagonal2, connectionPoints, threshold);
            
            Console.WriteLine($"Diagonal 1 score: {diagonal1Score:F2}");
            Console.WriteLine($"Diagonal 2 score: {diagonal2Score:F2}");
            
            if (diagonal1Score == 0 && diagonal2Score == 0)
            {
                Console.WriteLine("No connection points close to either diagonal.");
                return null;
            }
            
            // Return the diagonal with the better score
            if (diagonal1Score >= diagonal2Score)
            {
                Console.WriteLine("Selecting diagonal 1 (top-left to bottom-right)");
                return diagonal1;
            }
            else
            {
                Console.WriteLine("Selecting diagonal 2 (top-right to bottom-left)");
                return diagonal2;
            }
        }

        private static float CalculateDiagonalScore(Line diagonal, List<Coordinate> connectionPoints, float threshold)
        {
            float score = 0f;
            int proximityCount = 0;
            float totalDistance = 0f;
            
            // First check if both endpoints are near connection points (this is crucial)
            float startConnectionDistance = float.MaxValue;
            float endConnectionDistance = float.MaxValue;
            
            foreach (var point in connectionPoints)
            {
                float distanceToStart = Coordinate.Distance(diagonal.Start, point);
                float distanceToEnd = Coordinate.Distance(diagonal.End, point);
                
                if (distanceToStart < startConnectionDistance)
                    startConnectionDistance = distanceToStart;
                    
                if (distanceToEnd < endConnectionDistance)
                    endConnectionDistance = distanceToEnd;
            }
            
            // Both endpoints must be reasonably close to connection points
            float endpointThreshold = threshold * 1.5f; // Allow slightly more tolerance for endpoints
            bool startConnected = startConnectionDistance < endpointThreshold;
            bool endConnected = endConnectionDistance < endpointThreshold;
            
            if (verboseMode)
            {
                Console.WriteLine($"  Start endpoint distance to nearest connection: {startConnectionDistance:F1} (connected: {startConnected})");
                Console.WriteLine($"  End endpoint distance to nearest connection: {endConnectionDistance:F1} (connected: {endConnected})");
            }
            
            // If endpoints don't connect well, heavily penalize this diagonal
            if (!startConnected || !endConnected)
            {
                if (verboseMode)
                {
                    Console.WriteLine($"  Diagonal rejected: endpoints not properly connected to nodes/lines");
                }
                return 0f; // Return 0 score if endpoints don't connect properly
            }
            
            // Give bonus score for well-connected endpoints
            float endpointScore = 0f;
            endpointScore += (endpointThreshold - startConnectionDistance) / endpointThreshold;
            endpointScore += (endpointThreshold - endConnectionDistance) / endpointThreshold;
            score += endpointScore * 2f; // Strong bonus for good endpoint connections
            
            if (verboseMode)
            {
                Console.WriteLine($"  Endpoint connection score: {endpointScore * 2f:F2}");
            }
            
            // Now check overall line proximity to connection points
            foreach (var point in connectionPoints)
            {
                float distance = diagonal.DistancePointToLine(point);
                
                if (distance < threshold)
                {
                    proximityCount++;
                    totalDistance += distance;
                    
                    // Give higher score for closer points (inverse relationship)
                    float proximityScore = (threshold - distance) / threshold;
                    score += proximityScore;
                    
                    if (verboseMode)
                    {
                        Console.WriteLine($"  Point ({point.X:F1},{point.Y:F1}) distance={distance:F1}, score contribution={proximityScore:F2}");
                    }
                }
            }
            
            // Add bonus for having multiple connection points
            if (proximityCount > 1)
            {
                score += proximityCount * 0.5f; // Bonus for each additional connection point
            }
            
            // Add penalty for high average distance (prefer tighter fits)
            if (proximityCount > 0)
            {
                float avgDistance = totalDistance / proximityCount;
                float distancePenalty = avgDistance / threshold; // Penalty proportional to average distance
                score -= distancePenalty * 0.3f; // Moderate penalty for being farther from points
                
                if (verboseMode)
                {
                    Console.WriteLine($"  Avg distance: {avgDistance:F2}, distance penalty: {distancePenalty * 0.3f:F2}");
                }
            }
            
            return score;
        }

        private static bool CheckEndpointConnection(Coordinate endpoint, List<Coordinate> connectionPoints, float threshold)
        {
            float endpointThreshold = threshold * 1.5f; // Allow slightly more tolerance for endpoint connections
            
            foreach (var point in connectionPoints)
            {
                float distance = Coordinate.Distance(endpoint, point);
                if (distance < endpointThreshold)
                {
                    if (verboseMode)
                    {
                        Console.WriteLine($"    Endpoint ({endpoint.X:F1},{endpoint.Y:F1}) connected to point ({point.X:F1},{point.Y:F1}), distance: {distance:F1}");
                    }
                    return true;
                }
            }
            
            if (verboseMode)
            {
                Console.WriteLine($"    Endpoint ({endpoint.X:F1},{endpoint.Y:F1}) not connected to any points (threshold: {endpointThreshold:F1})");
            }
            return false;
        }
    }
}