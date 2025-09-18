namespace Calculations
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using YoloDetect;

    public class CalculateMissingNodes
    {
        private readonly List<Coordinate> nodesAndPylons;
        private readonly float nodeProximityThreshold;
        private readonly float missingNodeSeparationThreshold;

        public CalculateMissingNodes(List<Coordinate> nodesAndPylons, float nodeProximityThreshold = 15f)
        {
            this.nodesAndPylons = nodesAndPylons;
            this.nodeProximityThreshold = nodeProximityThreshold+25f;
            this.missingNodeSeparationThreshold = nodeProximityThreshold * 10f; // Missing nodes must be at least 80% of proximity threshold apart
        }

        public List<DetectedObject> GetMissingNodes(List<DetectedObject> detectedObjects, List<Line> drawnLines)
        {
            var missingNodes = new List<DetectedObject>();
            var candidateMissingNodes = new List<Coordinate>();
            
            // Get existing pylons and traffic cones from detected objects
            var existingPylonsAndCones = detectedObjects
                .Where(obj => obj.ClassName.Equals("pylon", StringComparison.OrdinalIgnoreCase) ||
                             obj.ClassName.Equals("pylons", StringComparison.OrdinalIgnoreCase) ||
                             obj.ClassName.Equals("traffic_cone", StringComparison.OrdinalIgnoreCase))
                .Select(obj => obj.BoundingBox.GetLowerCenter()) // Use the bottom center for pylons/cones
                .ToList();

            var existingNodes = detectedObjects
                .Where(obj => obj.ClassName.Equals("node", StringComparison.OrdinalIgnoreCase))
                .Select(obj => obj.BoundingBox.GetCenter()) // Use the center for nodes
                .ToList();

            // Get existing barriers as well
            var existingBarriers = detectedObjects
                .Where(obj => obj.ClassName.Equals("barrier", StringComparison.OrdinalIgnoreCase) ||
                             obj.ClassName.Equals("barrier_white", StringComparison.OrdinalIgnoreCase) ||
                             obj.ClassName.Equals("barrier_red", StringComparison.OrdinalIgnoreCase))
                .Select(obj => obj.BoundingBox.GetCenter())
                .ToList();

            // Combine all existing connection points
            var allExistingConnectionPoints = existingPylonsAndCones
                .Concat(existingNodes)
                .Concat(existingBarriers)
                .ToList();

            Console.WriteLine($"Found {existingPylonsAndCones.Count} pylons/cones, {existingNodes.Count} nodes, {existingBarriers.Count} barriers");
            
            // Find line intersections
            var intersectionPoints = FindLineIntersections(drawnLines);
            
            // Find line endpoints that don't connect to existing connection points
            var unconnectedEndpoints = FindUnconnectedEndpoints(drawnLines, allExistingConnectionPoints);
            
            // Combine all potential node locations
            var allPotentialNodes = intersectionPoints.Concat(unconnectedEndpoints).ToList();
            
            Console.WriteLine($"Found {intersectionPoints.Count} intersection points and {unconnectedEndpoints.Count} unconnected endpoints");
            
            // Filter out points that are too close to existing connection points
            foreach (var point in allPotentialNodes)
            {
                bool tooCloseToExisting = false;
                
                // Check distance to all existing connection points (pylons, cones, nodes, barriers)
                foreach (var existingPoint in allExistingConnectionPoints)
                {
                    float distance = Coordinate.Distance(point, existingPoint);
                    if (distance < nodeProximityThreshold)
                    {
                        tooCloseToExisting = true;
                        Console.WriteLine($"Potential node at ({point.X:F1},{point.Y:F1}) too close to existing point at ({existingPoint.X:F1},{existingPoint.Y:F1}), distance: {distance:F1}");
                        break;
                    }
                }

                // Check distance to existing candidate nodes
                if (!tooCloseToExisting)
                {
                    foreach (var candidate in candidateMissingNodes)
                    {
                        float distance = Coordinate.Distance(point, candidate);
                        if (distance < missingNodeSeparationThreshold)
                        {
                            tooCloseToExisting = true;
                            Console.WriteLine($"Potential node at ({point.X:F1},{point.Y:F1}) too close to candidate at ({candidate.X:F1},{candidate.Y:F1}), distance: {distance:F1}");
                            break;
                        }
                    }
                }
                
                // Check distance to nodes and pylons from the original detection list (from constructor)
                if (!tooCloseToExisting)
                {
                    foreach (var node in nodesAndPylons)
                    {
                        float distance = Coordinate.Distance(point, node);
                        if (distance < nodeProximityThreshold)
                        {
                            tooCloseToExisting = true;
                            Console.WriteLine($"Potential node at ({point.X:F1},{point.Y:F1}) too close to original detection at ({node.X:F1},{node.Y:F1}), distance: {distance:F1}");
                            break;
                        }
                    }
                }
                
                // Check if point is actually at a line end or intersection
                if (!tooCloseToExisting && IsPointAtLineEndOrIntersection(point, drawnLines))
                {
                    candidateMissingNodes.Add(point);
                }
            }

            // Filter candidate missing nodes to ensure they're not too close to each other
            var finalMissingNodes = FilterMissingNodesByProximity(candidateMissingNodes);

            // Create DetectedObject instances for the final missing nodes
            Console.WriteLine($"Creating {finalMissingNodes.Count} missing node objects from candidates");
            foreach (var point in finalMissingNodes)
            {
                Console.WriteLine($"Creating missing node at ({point.X:F1},{point.Y:F1})");
                var topLeft = new Coordinate(point.X - 5, point.Y - 5);
                var topRight = new Coordinate(point.X + 5, point.Y - 5);
                var bottomLeft = new Coordinate(point.X - 5, point.Y + 5);
                var bottomRight = new Coordinate(point.X + 5, point.Y + 5);
                
                var missingNode = new DetectedObject(
                    classId: -1, // Special ID for missing nodes
                    confidence: 0.9, // High confidence for calculated nodes
                    boundingBox: new BoundingBox(topLeft, topRight, bottomLeft, bottomRight),
                    className: "Node",
                    detectionId: Guid.NewGuid().ToString()
                );
                missingNodes.Add(missingNode);
            }

            Console.WriteLine($"Final result: {missingNodes.Count} missing nodes generated");
            return missingNodes;
        }

        private List<Coordinate> FilterMissingNodesByProximity(List<Coordinate> candidateNodes)
        {
            var filteredNodes = new List<Coordinate>();
            var processedNodes = new HashSet<int>();

            // Sort nodes by priority (intersections first, then endpoints)
            var sortedCandidates = candidateNodes
                .Select((node, index) => new { Node = node, Index = index })
                .OrderByDescending(x => GetNodePriority(x.Node))
                .ToList();

            foreach (var candidate in sortedCandidates)
            {
                if (processedNodes.Contains(candidate.Index)) continue;

                bool tooCloseToSelected = false;
                
                // Check if too close to already selected nodes
                foreach (var selectedNode in filteredNodes)
                {
                    float distance = Coordinate.Distance(candidate.Node, selectedNode);
                    
                    if (distance < missingNodeSeparationThreshold)
                    {
                        tooCloseToSelected = true;
                        break;
                    }
                }

                if (!tooCloseToSelected)
                {
                    filteredNodes.Add(candidate.Node);
                    
                    // Mark nearby nodes as processed to avoid clustering
                    for (int i = 0; i < candidateNodes.Count; i++)
                    {
                        if (processedNodes.Contains(i)) continue;
                        
                        float distance = Coordinate.Distance(candidate.Node, candidateNodes[i]);
                        
                        if (distance < missingNodeSeparationThreshold)
                        {
                            processedNodes.Add(i);
                        }
                    }
                }
                
                processedNodes.Add(candidate.Index);
            }

            return filteredNodes;
        }

        private int GetNodePriority(Coordinate point)
        {
            // Higher priority for intersections (where multiple lines cross)
            // Lower priority for simple endpoints
            // This could be enhanced to actually count line intersections at this point
            return 1; // For now, all have same priority
        }

        private bool IsPointAtLineEndOrIntersection(Coordinate point, List<Line> lines)
        {
            float tolerance = 8f; // Slightly larger tolerance for line end detection
            
            int lineCount = 0;
            List<string> nearbyLines = new List<string>();
            
            foreach (var line in lines)
            {
                // Check if point is near start of line
                float distanceToStart = Coordinate.Distance(point, line.Start);
                
                // Check if point is near end of line
                float distanceToEnd = Coordinate.Distance(point, line.End);
                
                if (distanceToStart <= tolerance || distanceToEnd <= tolerance)
                {
                    lineCount++;
                    nearbyLines.Add(line.Name);
                }
            }
            
            // Require at least 2 lines meeting for a missing node (intersection point)
            // Single line endpoints are less likely to need missing nodes
            bool isValidNode = lineCount >= 2;
            
            if (isValidNode)
            {
                Console.WriteLine($"Valid missing node candidate at ({point.X:F1},{point.Y:F1}) with {lineCount} nearby lines: {string.Join(", ", nearbyLines)}");
            }
            
            return isValidNode;
        }

        private List<Coordinate> FindLineIntersections(List<Line> lines)
        {
            var intersections = new List<Coordinate>();
            
            for (int i = 0; i < lines.Count; i++)
            {
                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (TryGetLineIntersection(lines[i], lines[j], out Coordinate intersection))
                    {
                        intersections.Add(intersection);
                    }
                }
            }
            
            return intersections;
        }

        private List<Coordinate> FindUnconnectedEndpoints(List<Line> lines, List<Coordinate> connectionPoints)
        {
            var endpoints = new List<Coordinate>();
            
            foreach (var line in lines)
            {
                var startCoord = new Coordinate(line.Start.X, line.Start.Y);
                var endCoord = new Coordinate(line.End.X, line.End.Y);
                
                // Check if start point is not near any existing connection point
                if (!IsPointNearConnectionPoints(startCoord, connectionPoints))
                {
                    endpoints.Add(startCoord);
                }
                
                // Check if end point is not near any existing connection point
                if (!IsPointNearConnectionPoints(endCoord, connectionPoints))
                {
                    endpoints.Add(endCoord);
                }
            }
            
            // Group endpoints by proximity and keep only those where multiple lines meet
            var groupedEndpoints = new List<Coordinate>();
            var processedPoints = new HashSet<int>();
            
            for (int i = 0; i < endpoints.Count; i++)
            {
                if (processedPoints.Contains(i)) continue;
                
                var currentPoint = endpoints[i];
                var nearbyPoints = new List<int> { i };
                
                // Find all points near this one
                for (int j = i + 1; j < endpoints.Count; j++)
                {
                    if (processedPoints.Contains(j)) continue;
                    
                    float distance = Coordinate.Distance(currentPoint, endpoints[j]);
                    if (distance < nodeProximityThreshold / 3) // Closer threshold for grouping endpoints
                    {
                        nearbyPoints.Add(j);
                    }
                }
                
                // If multiple lines meet at this point, add it as a node
                if (nearbyPoints.Count >= 2)
                {
                    groupedEndpoints.Add(currentPoint);
                }
                
                // Mark all nearby points as processed
                foreach (var pointIndex in nearbyPoints)
                {
                    processedPoints.Add(pointIndex);
                }
            }
            
            return groupedEndpoints;
        }

        private bool IsPointNearConnectionPoints(Coordinate point, List<Coordinate> connectionPoints)
        {
            foreach (var connectionPoint in connectionPoints)
            {
                float distance = Coordinate.Distance(point, connectionPoint);
                if (distance < nodeProximityThreshold)
                {
                    return true;
                }
            }
            return false;
        }

        private bool TryGetLineIntersection(Line line1, Line line2, out Coordinate intersection)
        {
            intersection = new Coordinate(0, 0);
            
            // Line1 represented as a1x + b1y = c1
            float a1 = line1.End.Y - line1.Start.Y;
            float b1 = line1.Start.X - line1.End.X;
            float c1 = a1 * line1.Start.X + b1 * line1.Start.Y;
            
            // Line2 represented as a2x + b2y = c2
            float a2 = line2.End.Y - line2.Start.Y;
            float b2 = line2.Start.X - line2.End.X;
            float c2 = a2 * line2.Start.X + b2 * line2.Start.Y;
            
            float determinant = a1 * b2 - a2 * b1;
            
            // If lines are parallel
            if (Math.Abs(determinant) < 0.001f)
                return false;
                
            float x = (b2 * c1 - b1 * c2) / determinant;
            float y = (a1 * c2 - a2 * c1) / determinant;
            
            intersection = new Coordinate(x, y);
            
            // Check if intersection is within both line segments
            return IsPointOnLineSegment(line1, intersection) && IsPointOnLineSegment(line2, intersection);
        }

        private bool IsPointOnLineSegment(Line line, Coordinate point)
        {
            float tolerance = 1f;
            
            // Check if point is within bounding box of line segment
            bool withinBounds = point.X >= Math.Min(line.Start.X, line.End.X) - tolerance &&
                               point.X <= Math.Max(line.Start.X, line.End.X) + tolerance &&
                               point.Y >= Math.Min(line.Start.Y, line.End.Y) - tolerance &&
                               point.Y <= Math.Max(line.Start.Y, line.End.Y) + tolerance;
            
            return withinBounds;
        }
    }
}