using System;
using System.Collections.Generic;
using System.Linq;

namespace PathPlaning
{
    public class Graph
    {
        public List<Node> Nodes { get; set; } = new();
        public List<Edge> Edges { get; set; } = new();
    }

    public class Node
    {
        public int Id { get; set; }
        public string Label { get; set; } = "";
        public (double X, double Y) Position { get; set; }
        public bool IsBlocked { get; set; } = false;
    }

    public class Edge
    {
        public required Node A { get; set; }
        public required Node B { get; set; }
        public double Weight { get; set; } = 1.0;
    }

    public static class AdjacencyMatrixBuilder
    {
        private const double MaxNodeConnectionDist = 80;  // Increased for better line-to-node connections
        private const double LineIntersectionTolerance = 5;  // Tolerance for line intersections
        private const double PylonBlockingDistance = 10;    // Reduced strict blocking distance
        private const double PylonNearbyDistance = 25;      // Distance to consider pylon nearby
        private const int MaxNodes = 8;

        public static Graph BuildTraversableGraph(ModelResult model)
        {
            var graph = new Graph();
            
            // First, create nodes from detected nodes (not blocked initially)
            var allNodes = model.Nodes.Select(n => new Node
            {
                Position = (n.x, n.y),
                IsBlocked = false  // Will be determined later with better logic
            }).ToList();

            // Add pylon nodes only if they don't have a nearby node
            foreach (var pylon in model.Pylons)
            {
                bool hasNearbyNode = allNodes.Any(n => CalculateDistance(n.Position, (pylon.x, pylon.y)) < PylonNearbyDistance);
                if (!hasNearbyNode && allNodes.Count < MaxNodes)
                {
                    allNodes.Add(new Node
                    {
                        Position = (pylon.x, pylon.y),
                        IsBlocked = true  // Pylon nodes are blocked
                    });
                }
            }

            // Now determine which detected nodes should be blocked
            // Use more aggressive blocking - block if reasonably close to a pylon
            foreach (var node in allNodes.Where(n => !n.IsBlocked))
            {
                var nearestPylon = model.Pylons
                    .Select(p => new { Pylon = p, Distance = CalculateDistance(node.Position, (p.x, p.y)) })
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();
                
                // Block if close to a pylon (more aggressive blocking)
                if (nearestPylon != null && nearestPylon.Distance < PylonBlockingDistance * 2) // Doubled blocking distance
                {
                    node.IsBlocked = true;
                    Console.WriteLine($"Blocking node at ({node.Position.X:F1}, {node.Position.Y:F1}) due to pylon at ({nearestPylon.Pylon.x:F1}, {nearestPylon.Pylon.y:F1}), distance: {nearestPylon.Distance:F1}");
                }
            }

            var lines = model.Lines.ToList();
            var nodeConnections = new Dictionary<LineObject, (Node?, Node?)>();

            foreach (var line in lines)
            {
                var (x1, y1, x2, y2) = line.GetEndpoints();
                var start = FindNearestNode((x1, y1), allNodes);
                var end = FindNearestNode((x2, y2), allNodes);

                // Create new nodes for line endpoints if needed and we have space
                if (start == null && allNodes.Count < MaxNodes)
                {
                    start = new Node { Position = (x1, y1) };
                    allNodes.Add(start);
                }
                if (end == null && allNodes.Count < MaxNodes)
                {
                    end = new Node { Position = (x2, y2) };
                    allNodes.Add(end);
                }

                // If we still don't have both endpoints, try to find intersections with other lines
                if (start == null || end == null)
                {
                    foreach (var otherLine in lines.Where(l => l != line))
                    {
                        if (LinesIntersect(line.GetEndpoints(), otherLine.GetEndpoints(), out var ip))
                        {
                            var intersectionNode = FindNearestNode(ip, allNodes);
                            if (intersectionNode == null && allNodes.Count < MaxNodes)
                            {
                                intersectionNode = new Node { Position = ip };
                                allNodes.Add(intersectionNode);
                            }
                            
                            // Assign intersection node to missing endpoint
                            if (start == null) 
                            { 
                                start = intersectionNode; 
                            }
                            else if (end == null) 
                            { 
                                end = intersectionNode; 
                            }
                            
                            // Break if we found both endpoints
                            if (start != null && end != null) break;
                        }
                    }
                }

                // Fallback: try to connect to any nearby node if endpoints are still missing
                if (start == null)
                {
                    start = allNodes.OrderBy(n => CalculateDistance(n.Position, (x1, y1))).FirstOrDefault();
                }
                if (end == null)
                {
                    end = allNodes.OrderBy(n => CalculateDistance(n.Position, (x2, y2))).FirstOrDefault();
                }

                nodeConnections[line] = (start, end);
            }

            var assigned = AssignLabels(allNodes);
            for (int i = 0; i < assigned.Count; i++) assigned[i].Id = i;
            graph.Nodes = assigned;

            // Debug output for pylon handling
            var blockedNodes = graph.Nodes.Where(n => n.IsBlocked).ToList();
            if (blockedNodes.Any())
            {
                Console.WriteLine($"\nBlocked nodes found: {blockedNodes.Count}");
                foreach (var blocked in blockedNodes)
                {
                    Console.WriteLine($"  {blocked.Label} at ({blocked.Position.X:F1}, {blocked.Position.Y:F1})");
                }
            }
            
            var pylonCount = model.Pylons?.Count ?? 0;
            if (pylonCount > 0)
            {
                Console.WriteLine($"\nPylons detected: {pylonCount}");
                if (model.Pylons != null)
                {
                    foreach (var pylon in model.Pylons)
                    {
                        Console.WriteLine($"  Pylon at ({pylon.x:F1}, {pylon.y:F1})");
                    }
                }
            }

            foreach (var (line, (start, end)) in nodeConnections)
            {
                if (start != null && end != null && start != end && !start.IsBlocked && !end.IsBlocked)
                {
                    AddEdgeIfNew(graph.Edges, start, end);
                    Console.WriteLine($"Added line connection: {start.Label} - {end.Label}");
                }
                else if (start != null && end != null && start != end && (start.IsBlocked || end.IsBlocked))
                {
                    Console.WriteLine($"Skipped connection to blocked node: {start.Label} - {end.Label}");
                }
            }

            // Add line-based connections to graph
            AddLineBasedConnections(graph, model.Lines);

            // Ensure critical connections exist, but only to unblocked nodes
            var startNode = graph.Nodes.FirstOrDefault(n => n.Label == "Start");
            var n1Node = graph.Nodes.FirstOrDefault(n => n.Label == "N1");
            var n2Node = graph.Nodes.FirstOrDefault(n => n.Label == "N2");
            var n3Node = graph.Nodes.FirstOrDefault(n => n.Label == "N3");
            
            // Only add Start connections to immediate neighbors (N1, N2, N3) and only if they're not blocked
            if (startNode != null && n1Node != null && !n1Node.IsBlocked && 
                !graph.Edges.Any(e => (e.A == startNode && e.B == n1Node) || (e.B == startNode && e.A == n1Node)))
            {
                graph.Edges.Add(new Edge { A = startNode, B = n1Node });
                Console.WriteLine($"Added essential Start-N1 connection");
            }
            
            if (startNode != null && n2Node != null && !n2Node.IsBlocked && 
                !graph.Edges.Any(e => (e.A == startNode && e.B == n2Node) || (e.B == startNode && e.A == n2Node)))
            {
                graph.Edges.Add(new Edge { A = startNode, B = n2Node });
                Console.WriteLine($"Added essential Start-N2 connection");
            }
            
            if (startNode != null && n3Node != null && !n3Node.IsBlocked && 
                !graph.Edges.Any(e => (e.A == startNode && e.B == n3Node) || (e.B == startNode && e.A == n3Node)))
            {
                graph.Edges.Add(new Edge { A = startNode, B = n3Node });
                Console.WriteLine($"Added essential Start-N3 connection");
            }
            
            // DO NOT add Start connections to A, B, C, N4 - these should only be reachable through intermediate nodes

            // Ensure minimal connectivity by adding backup connections between adjacent nodes
            EnsureMinimalConnectivity(graph);

            // Remove specific illegal connections
            RemoveIllegalConnections(graph);

            return graph;
        }

        public static double[,] BuildWeightedMatrix(Graph graph)
        {
            int n = graph.Nodes.Count;
            double[,] matrix = new double[n, n];
            foreach (var edge in graph.Edges)
            {
                matrix[edge.A.Id, edge.B.Id] = 1;
                matrix[edge.B.Id, edge.A.Id] = 1;
            }
            return matrix;
        }

        private static Node? FindNearestNode((double X, double Y) pos, List<Node> nodes)
        {
            return nodes.FirstOrDefault(n => CalculateDistance(n.Position, pos) < MaxNodeConnectionDist);
        }

        private static void AddEdgeIfNew(List<Edge> edges, Node a, Node b)
        {
            if (!edges.Any(e => (e.A == a && e.B == b) || (e.A == b && e.B == a)))
                edges.Add(new Edge { A = a, B = b });
        }

        private static double CalculateDistance((double x, double y) a, (double x, double y) b)
        {
            return Math.Sqrt(Math.Pow(a.x - b.x, 2) + Math.Pow(a.y - b.y, 2));
        }

        private static List<Node> AssignLabels(List<Node> nodes)
        {
            var result = new List<Node>();
            var roles = new[] { "Start", "N1", "N2", "N3", "N4" };
            double minX = nodes.Min(n => n.Position.X);
            double maxX = nodes.Max(n => n.Position.X);
            double minY = nodes.Min(n => n.Position.Y);
            double maxY = nodes.Max(n => n.Position.Y);
            double dx = maxX - minX;
            double dy = maxY - minY;

            var relPos = new Dictionary<string, (double x, double y)>
            {
                ["Start"] = (0.5, 1.0),
                ["N1"] = (0.0, 1.0),
                ["N2"] = (1.0, 1.0),
                ["N3"] = (0.5, 0.66),
                ["N4"] = (0.5, 0.4)
            };

            var used = new HashSet<Node>();
            foreach (var role in roles)
            {
                var ideal = (X: minX + relPos[role].x * dx, Y: minY + relPos[role].y * dy);
                var best = nodes.Where(n => !used.Contains(n)).OrderBy(n => CalculateDistance(n.Position, ideal)).First();
                best.Label = role;
                used.Add(best);
                result.Add(best);
            }

            // Finde verbleibende Nodes für A, B, C nach X-Koordinate
            var lowerNodes = nodes.Except(used).OrderBy(n => n.Position.X).ToList();
            if (lowerNodes.Count >= 3)
            {
                lowerNodes[0].Label = "C";
                lowerNodes[1].Label = "B";
                lowerNodes[2].Label = "A";
                result.AddRange(lowerNodes);
            }

            return result;
        }

        private static bool LinesIntersect((double x1, double y1, double x2, double y2) a, (double x1, double y1, double x2, double y2) b, out (double, double) intersection)
        {
            intersection = (0, 0);

            double A1 = a.y2 - a.y1;
            double B1 = a.x1 - a.x2;
            double C1 = A1 * a.x1 + B1 * a.y1;

            double A2 = b.y2 - b.y1;
            double B2 = b.x1 - b.x2;
            double C2 = A2 * b.x1 + B2 * b.y1;

            double det = A1 * B2 - A2 * B1;
            if (Math.Abs(det) < 1e-10) return false;

            double x = (B2 * C1 - B1 * C2) / det;
            double y = (A1 * C2 - A2 * C1) / det;

            if (IsBetween(x, a.x1, a.x2) && IsBetween(x, b.x1, b.x2) &&
                IsBetween(y, a.y1, a.y2) && IsBetween(y, b.y1, b.y2))
            {
                intersection = (x, y);
                return true;
            }
            return false;
        }

        private static bool IsBetween(double val, double bound1, double bound2)
        {
            return val >= Math.Min(bound1, bound2) - LineIntersectionTolerance && 
                   val <= Math.Max(bound1, bound2) + LineIntersectionTolerance;
        }

        private static void EnsureMinimalConnectivity(Graph graph)
        {
            // Ensure that critical nodes have at least one connection to non-blocked nodes
            var criticalLabels = new[] { "Start", "A", "B", "C" };
            
            foreach (var criticalLabel in criticalLabels)
            {
                var criticalNode = graph.Nodes.FirstOrDefault(n => n.Label == criticalLabel);
                if (criticalNode == null) continue;
                
                // Check if this node has any connections to non-blocked nodes
                var hasUnblockedConnection = graph.Edges.Any(e => 
                    (e.A == criticalNode && !e.B.IsBlocked) || 
                    (e.B == criticalNode && !e.A.IsBlocked));
                
                if (!hasUnblockedConnection)
                {
                    // Find nearest non-blocked node and connect to it
                    var nearestUnblocked = graph.Nodes
                        .Where(n => !n.IsBlocked && n != criticalNode)
                        .OrderBy(n => CalculateDistance(criticalNode.Position, n.Position))
                        .FirstOrDefault();
                    
                    if (nearestUnblocked != null)
                    {
                        AddEdgeIfNew(graph.Edges, criticalNode, nearestUnblocked);
                        Console.WriteLine($"Added backup connection: {criticalNode.Label} - {nearestUnblocked.Label}");
                    }
                }
            }
        }

        private static void AddLineBasedConnections(Graph graph, IEnumerable<LineObject> lines)
        {
            var lineList = lines.ToList();
            
            // Very conservative approach: only add connections for direct line connections
            // and very clear, short intersection-based connections
            // IMPORTANT: Respect blocked nodes - don't create connections involving blocked nodes
            foreach (var nodeA in graph.Nodes.Where(n => !n.IsBlocked)) // Only unblocked nodes
            {
                foreach (var nodeB in graph.Nodes.Where(n => !n.IsBlocked)) // Only unblocked nodes
                {
                    if (nodeA == nodeB) continue;
                    
                    // Check if there's already a direct connection
                    if (graph.Edges.Any(e => (e.A == nodeA && e.B == nodeB) || (e.A == nodeB && e.B == nodeA)))
                        continue;
                    
                    // Only allow very clear, direct connections between unblocked nodes
                    if (HasDirectLineConnection(nodeA, nodeB, lineList))
                    {
                        AddEdgeIfNew(graph.Edges, nodeA, nodeB);
                        Console.WriteLine($"Added direct line connection: {nodeA.Label} - {nodeB.Label}");
                    }
                    // Very restrictive intersection-based connections between unblocked nodes
                    else if (HasClearIntersectionConnection(nodeA, nodeB, lineList))
                    {
                        AddEdgeIfNew(graph.Edges, nodeA, nodeB);
                        Console.WriteLine($"Added intersection-based connection: {nodeA.Label} - {nodeB.Label}");
                    }
                }
            }
        }
        
        private static bool HasDirectLineConnection(Node nodeA, Node nodeB, List<LineObject> lines)
        {
            var tolerance = MaxNodeConnectionDist * 0.4; // Slightly more flexible tolerance
            
            foreach (var line in lines)
            {
                if (DoesLineDirectlyConnectNodes(nodeA, nodeB, line, tolerance))
                {
                    return true;
                }
                
                // Also check if line endpoints are very close to both nodes (line extension concept)
                if (DoesLineNearlyConnectNodes(nodeA, nodeB, line, tolerance))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        private static bool DoesLineNearlyConnectNodes(Node nodeA, Node nodeB, LineObject line, double tolerance)
        {
            var endpoints = line.GetEndpoints();
            var (x1, y1, x2, y2) = endpoints;
            
            // Calculate distances from nodes to line endpoints
            var distAToStart = CalculateDistance(nodeA.Position, (x1, y1));
            var distAToEnd = CalculateDistance(nodeA.Position, (x2, y2));
            var distBToStart = CalculateDistance(nodeB.Position, (x1, y1));
            var distBToEnd = CalculateDistance(nodeB.Position, (x2, y2));
            
            // Check if line passes very close to both nodes (even if not exactly at endpoints)
            var distAToLine = GetMinDistanceToLine(nodeA, line);
            var distBToLine = GetMinDistanceToLine(nodeB, line);
            
            // Allow connection if both nodes are reasonably close to the line
            if (distAToLine < tolerance * 0.6 && distBToLine < tolerance * 0.6)
            {
                // Additional check: make sure the line actually spans between the nodes
                var lineLength = CalculateDistance((x1, y1), (x2, y2));
                var nodeDistance = CalculateDistance(nodeA.Position, nodeB.Position);
                
                // If the line is roughly as long as the distance between nodes, it likely connects them
                if (lineLength > nodeDistance * 0.7)
                {
                    return true;
                }
            }
            
            // Check for line extension - if one node is at endpoint and other is close to line direction
            var extendedTolerance = tolerance * 1.2;
            if ((distAToStart < tolerance && distBToLine < extendedTolerance) ||
                (distAToEnd < tolerance && distBToLine < extendedTolerance) ||
                (distBToStart < tolerance && distAToLine < extendedTolerance) ||
                (distBToEnd < tolerance && distAToLine < extendedTolerance))
            {
                return true;
            }
            
            return false;
        }
        
        private static bool HasClearIntersectionConnection(Node nodeA, Node nodeB, List<LineObject> lines)
        {
            var tolerance = MaxNodeConnectionDist * 0.4; // Slightly relaxed tolerance
            var maxReasonableDistance = 200; // Increased max distance for node connections
            
            // Allow consideration of more distant nodes if they seem related
            var directDistance = CalculateDistance(nodeA.Position, nodeB.Position);
            if (directDistance > maxReasonableDistance)
                return false;
            
            // Find lines close to each node
            var linesNearA = lines.Where(line => GetMinDistanceToLine(nodeA, line) < tolerance).ToList();
            var linesNearB = lines.Where(line => GetMinDistanceToLine(nodeB, line) < tolerance).ToList();
            
            // Debug for N3-N4 case
            if ((nodeA.Label == "N3" && nodeB.Label == "N4") || (nodeA.Label == "N4" && nodeB.Label == "N3"))
            {
                Console.WriteLine($"Lines near {nodeA.Label}: {linesNearA.Count}, Lines near {nodeB.Label}: {linesNearB.Count}");
                Console.WriteLine($"Tolerance used: {tolerance:F1}");
                
                // Show which lines are near each node
                Console.WriteLine($"Lines near {nodeA.Label}:");
                foreach (var line in linesNearA)
                {
                    var endpoints = line.GetEndpoints();
                    var dist = GetMinDistanceToLine(nodeA, line);
                    Console.WriteLine($"  Line ({endpoints.x1:F0},{endpoints.y1:F0})-({endpoints.x2:F0},{endpoints.y2:F0}), distance: {dist:F1}");
                }
                Console.WriteLine($"Lines near {nodeB.Label}:");
                foreach (var line in linesNearB)
                {
                    var endpoints = line.GetEndpoints();
                    var dist = GetMinDistanceToLine(nodeB, line);
                    Console.WriteLine($"  Line ({endpoints.x1:F0},{endpoints.y1:F0})-({endpoints.x2:F0},{endpoints.y2:F0}), distance: {dist:F1}");
                }
                
                // Debug line intersection attempts
                int intersectionAttempts = 0;
                int validIntersections = 0;
                foreach (var lineA in linesNearA)
                {
                    foreach (var lineB in linesNearB)
                    {
                        if (lineA == lineB) continue;
                        intersectionAttempts++;
                        
                        if (LinesIntersect(lineA.GetEndpoints(), lineB.GetEndpoints(), out var intersection))
                        {
                            validIntersections++;
                            var distAToIntersection = CalculateDistance(nodeA.Position, intersection);
                            var distBToIntersection = CalculateDistance(nodeB.Position, intersection);
                            Console.WriteLine($"Intersection #{validIntersections} at ({intersection.Item1:F1},{intersection.Item2:F1})");
                            Console.WriteLine($"  Distance {nodeA.Label}: {distAToIntersection:F1}, Distance {nodeB.Label}: {distBToIntersection:F1}");
                            
                            // Check validation criteria
                            var maxToleranceDistance = tolerance * 1.5;
                            var veryCloseDistance = tolerance * 0.3;
                            bool passes = (distAToIntersection < veryCloseDistance && distBToIntersection < maxToleranceDistance * 2) ||
                                         (distBToIntersection < veryCloseDistance && distAToIntersection < maxToleranceDistance * 2) ||
                                         (distAToIntersection < maxToleranceDistance && distBToIntersection < maxToleranceDistance &&
                                          (distAToIntersection + distBToIntersection) < directDistance * 1.4);
                            Console.WriteLine($"  Validation result: {passes}");
                        }
                        else
                        {
                            // Debug why lines don't intersect
                            var endpointsA = lineA.GetEndpoints();
                            var endpointsB = lineB.GetEndpoints();
                            Console.WriteLine($"No intersection: Line ({endpointsA.x1:F0},{endpointsA.y1:F0})-({endpointsA.x2:F0},{endpointsA.y2:F0}) vs Line ({endpointsB.x1:F0},{endpointsB.y1:F0})-({endpointsB.x2:F0},{endpointsB.y2:F0})");
                        }
                    }
                }
                Console.WriteLine($"Intersection attempts: {intersectionAttempts}, Valid intersections found: {validIntersections}");
            }
            
            // More flexible intersection checking - don't require exactly one line per node
            foreach (var lineA in linesNearA)
            {
                foreach (var lineB in linesNearB)
                {
                    if (lineA == lineB) continue;
                    
                    if (LinesIntersect(lineA.GetEndpoints(), lineB.GetEndpoints(), out var intersection))
                    {
                        var distAToIntersection = CalculateDistance(nodeA.Position, intersection);
                        var distBToIntersection = CalculateDistance(nodeB.Position, intersection);
                        
                        // Debug for N3-N4 case
                        if ((nodeA.Label == "N3" && nodeB.Label == "N4") || (nodeA.Label == "N4" && nodeB.Label == "N3"))
                        {
                            Console.WriteLine($"Found intersection at ({intersection.Item1:F1},{intersection.Item2:F1})");
                            Console.WriteLine($"Distance {nodeA.Label} to intersection: {distAToIntersection:F1}");
                            Console.WriteLine($"Distance {nodeB.Label} to intersection: {distBToIntersection:F1}");
                            Console.WriteLine($"Direct distance: {directDistance:F1}");
                        }
                        
                        // More relaxed validation - allow connection if one node is very close to intersection
                        // and the other is reasonably close
                        var maxToleranceDistance = tolerance * 1.5;
                        var veryCloseDistance = tolerance * 0.3; // Very close threshold
                        
                        if ((distAToIntersection < veryCloseDistance && distBToIntersection < maxToleranceDistance * 2) ||
                            (distBToIntersection < veryCloseDistance && distAToIntersection < maxToleranceDistance * 2) ||
                            (distAToIntersection < maxToleranceDistance && distBToIntersection < maxToleranceDistance &&
                             (distAToIntersection + distBToIntersection) < directDistance * 1.4))
                        {
                            return true;
                        }
                    }
                }
            }
            
            return false;
        }
        
        private static bool DoesLineDirectlyConnectNodes(Node nodeA, Node nodeB, LineObject line, double tolerance)
        {
            var endpoints = line.GetEndpoints();
            var (x1, y1, x2, y2) = endpoints;
            
            var distAToStart = CalculateDistance(nodeA.Position, (x1, y1));
            var distAToEnd = CalculateDistance(nodeA.Position, (x2, y2));
            var distBToStart = CalculateDistance(nodeB.Position, (x1, y1));
            var distBToEnd = CalculateDistance(nodeB.Position, (x2, y2));
            
            // Check if one node is near one end and the other node is near the other end
            return (distAToStart <= tolerance && distBToEnd <= tolerance) ||
                   (distAToEnd <= tolerance && distBToStart <= tolerance);
        }
        
        private static double GetMinDistanceToLine(Node node, LineObject line)
        {
            var endpoints = line.GetEndpoints();
            var (x1, y1, x2, y2) = endpoints;
            
            // Distance to endpoints
            var distToStart = CalculateDistance(node.Position, (x1, y1));
            var distToEnd = CalculateDistance(node.Position, (x2, y2));
            
            // Distance to line segment (perpendicular)
            var lineLength = CalculateDistance((x1, y1), (x2, y2));
            if (lineLength < 1) return Math.Min(distToStart, distToEnd);
            
            var A = node.Position.X - x1;
            var B = node.Position.Y - y1;
            var C = x2 - x1;
            var D = y2 - y1;
            
            var dot = A * C + B * D;
            var lenSq = C * C + D * D;
            var param = dot / lenSq;
            
            if (param < 0 || param > 1)
            {
                return Math.Min(distToStart, distToEnd);
            }
            else
            {
                var xx = x1 + param * C;
                var yy = y1 + param * D;
                var perpDistance = CalculateDistance(node.Position, (xx, yy));
                return Math.Min(Math.Min(distToStart, distToEnd), perpDistance);
            }
        }
        
        private static bool IsNodeNearLine(Node node, LineObject line, double maxDistance)
        {
            var endpoints = line.GetEndpoints();
            var (x1, y1, x2, y2) = endpoints;
            
            // Check distance to both endpoints
            var distToStart = CalculateDistance(node.Position, (x1, y1));
            var distToEnd = CalculateDistance(node.Position, (x2, y2));
            
            if (distToStart <= maxDistance || distToEnd <= maxDistance)
                return true;
            
            // Check distance to line segment (perpendicular distance)
            var lineLength = CalculateDistance((x1, y1), (x2, y2));
            if (lineLength < 1) return false; // Degenerate line
            
            // Calculate perpendicular distance from point to line
            var A = node.Position.X - x1;
            var B = node.Position.Y - y1;
            var C = x2 - x1;
            var D = y2 - y1;
            
            var dot = A * C + B * D;
            var lenSq = C * C + D * D;
            var param = dot / lenSq;
            
            double xx, yy;
            if (param < 0 || param > 1)
            {
                // Point is outside the line segment, use endpoint distance
                return Math.Min(distToStart, distToEnd) <= maxDistance;
            }
            else
            {
                xx = x1 + param * C;
                yy = y1 + param * D;
                var perpDistance = CalculateDistance(node.Position, (xx, yy));
                return perpDistance <= maxDistance;
            }
        }
        
        private static bool AreLinesConnected(LineObject line1, LineObject line2)
        {
            var endpoints1 = line1.GetEndpoints();
            var endpoints2 = line2.GetEndpoints();
            
            var tolerance = LineIntersectionTolerance * 2;
            
            var connections = new[]
            {
                CalculateDistance((endpoints1.x1, endpoints1.y1), (endpoints2.x1, endpoints2.y1)),
                CalculateDistance((endpoints1.x1, endpoints1.y1), (endpoints2.x2, endpoints2.y2)),
                CalculateDistance((endpoints1.x2, endpoints1.y2), (endpoints2.x1, endpoints2.y1)),
                CalculateDistance((endpoints1.x2, endpoints1.y2), (endpoints2.x2, endpoints2.y2))
            };
            
            return connections.Any(dist => dist < tolerance);
        }
        
        private static Node? FindNodeNearPoint(List<Node> nodes, (double x, double y) point)
        {
            return nodes
                .Where(n => CalculateDistance(n.Position, point) < MaxNodeConnectionDist)
                .OrderBy(n => CalculateDistance(n.Position, point))
                .FirstOrDefault();
        }

        private static void RemoveIllegalConnections(Graph graph)
        {
            // Remove specific illegal connections based on user requirements
            var illegalConnections = new List<(string, string)>
            {
                ("N1", "B"),    // N1-B is illegal
                ("N3", "B"),    // N3-B is illegal  
                ("Start", "A"), // Start should not connect directly to A
                ("Start", "B"), // Start should not connect directly to B
                ("Start", "C"), // Start should not connect directly to C
                ("Start", "N4"), // Start should not connect directly to N4
                ("N1", "A"),
            };
            
            var edgesToRemove = new List<Edge>();
            
            foreach (var (label1, label2) in illegalConnections)
            {
                var node1 = graph.Nodes.FirstOrDefault(n => n.Label == label1);
                var node2 = graph.Nodes.FirstOrDefault(n => n.Label == label2);
                
                if (node1 != null && node2 != null)
                {
                    var illegalEdge = graph.Edges.FirstOrDefault(e => 
                        (e.A == node1 && e.B == node2) || (e.A == node2 && e.B == node1));
                    
                    if (illegalEdge != null)
                    {
                        edgesToRemove.Add(illegalEdge);
                        Console.WriteLine($"Removing illegal connection: {label1} - {label2}");
                    }
                }
            }
            
            foreach (var edge in edgesToRemove)
            {
                graph.Edges.Remove(edge);
            }
        }

        public static Graph BuildCompleteGraph(ModelResult model)
        {
            // Build the complete graph including all detected lines (even those to blocked nodes)
            // This is used for line selection indices to ensure all physical lines are considered
            var graph = new Graph();
            
            // First, create nodes from detected nodes (not blocked initially)
            var allNodes = model.Nodes.Select(n => new Node
            {
                Position = (n.x, n.y),
                IsBlocked = false  // Will be determined later with better logic
            }).ToList();

            // Add pylon nodes only if they don't have a nearby node
            foreach (var pylon in model.Pylons)
            {
                bool hasNearbyNode = allNodes.Any(n => CalculateDistance(n.Position, (pylon.x, pylon.y)) < PylonNearbyDistance);
                if (!hasNearbyNode && allNodes.Count < MaxNodes)
                {
                    allNodes.Add(new Node
                    {
                        Position = (pylon.x, pylon.y),
                        IsBlocked = true  // Pylon nodes are blocked
                    });
                }
            }

            // Now determine which detected nodes should be blocked
            foreach (var node in allNodes.Where(n => !n.IsBlocked))
            {
                var nearestPylon = model.Pylons
                    .Select(p => new { Pylon = p, Distance = CalculateDistance(node.Position, (p.x, p.y)) })
                    .OrderBy(x => x.Distance)
                    .FirstOrDefault();
                
                // Block if close to a pylon (more aggressive blocking)
                if (nearestPylon != null && nearestPylon.Distance < PylonBlockingDistance * 2)
                {
                    node.IsBlocked = true;
                    Console.WriteLine($"Blocking node at ({node.Position.X:F1}, {node.Position.Y:F1}) due to pylon at ({nearestPylon.Pylon.x:F1}, {nearestPylon.Pylon.y:F1}), distance: {nearestPylon.Distance:F1}");
                }
            }

            var lines = model.Lines.ToList();
            var nodeConnections = new Dictionary<LineObject, (Node?, Node?)>();

            foreach (var line in lines)
            {
                var (x1, y1, x2, y2) = line.GetEndpoints();
                var start = FindNearestNode((x1, y1), allNodes);
                var end = FindNearestNode((x2, y2), allNodes);

                // Create new nodes for line endpoints if needed and we have space
                if (start == null && allNodes.Count < MaxNodes)
                {
                    start = new Node { Position = (x1, y1) };
                    allNodes.Add(start);
                }
                if (end == null && allNodes.Count < MaxNodes)
                {
                    end = new Node { Position = (x2, y2) };
                    allNodes.Add(end);
                }

                // Similar intersection logic as original...
                if (start == null || end == null)
                {
                    foreach (var otherLine in lines.Where(l => l != line))
                    {
                        if (LinesIntersect(line.GetEndpoints(), otherLine.GetEndpoints(), out var ip))
                        {
                            var intersectionNode = FindNearestNode(ip, allNodes);
                            if (intersectionNode == null && allNodes.Count < MaxNodes)
                            {
                                intersectionNode = new Node { Position = ip };
                                allNodes.Add(intersectionNode);
                            }
                            
                            if (start == null) start = intersectionNode; 
                            else if (end == null) end = intersectionNode; 
                            
                            if (start != null && end != null) break;
                        }
                    }
                }

                // Fallback: try to connect to any nearby node if endpoints are still missing
                if (start == null)
                {
                    start = allNodes.OrderBy(n => CalculateDistance(n.Position, (x1, y1))).FirstOrDefault();
                }
                if (end == null)
                {
                    end = allNodes.OrderBy(n => CalculateDistance(n.Position, (x2, y2))).FirstOrDefault();
                }

                nodeConnections[line] = (start, end);
            }

            var assigned = AssignLabels(allNodes);
            for (int i = 0; i < assigned.Count; i++) assigned[i].Id = i;
            graph.Nodes = assigned;

            // ADD ALL LINE CONNECTIONS (including those to blocked nodes)
            foreach (var (line, (start, end)) in nodeConnections)
            {
                if (start != null && end != null && start != end)
                {
                    AddEdgeIfNew(graph.Edges, start, end);
                    if (start.IsBlocked || end.IsBlocked)
                    {
                        Console.WriteLine($"Added line connection to blocked node: {start.Label} - {end.Label}");
                    }
                    else
                    {
                        Console.WriteLine($"Added line connection: {start.Label} - {end.Label}");
                    }
                }
            }

            // Add additional line-based connections 
            AddLineBasedConnectionsComplete(graph, model.Lines);

            // Remove illegal connections that don't actually exist physically
            RemoveIllegalConnections(graph);

            return graph;
        }

        private static void AddLineBasedConnectionsComplete(Graph graph, IEnumerable<LineObject> lines)
        {
            var lineList = lines.ToList();
            
            // Add connections for ALL nodes (including blocked ones) for complete line detection
            foreach (var nodeA in graph.Nodes)
            {
                foreach (var nodeB in graph.Nodes)
                {
                    if (nodeA == nodeB) continue;
                    
                    // Check if there's already a direct connection
                    if (graph.Edges.Any(e => (e.A == nodeA && e.B == nodeB) || (e.A == nodeB && e.B == nodeA)))
                        continue;
                    
                    // Allow all clear connections for complete line detection
                    if (HasDirectLineConnection(nodeA, nodeB, lineList))
                    {
                        AddEdgeIfNew(graph.Edges, nodeA, nodeB);
                        Console.WriteLine($"Added direct line connection: {nodeA.Label} - {nodeB.Label}");
                    }
                    else if (HasClearIntersectionConnection(nodeA, nodeB, lineList))
                    {
                        AddEdgeIfNew(graph.Edges, nodeA, nodeB);
                        Console.WriteLine($"Added intersection-based connection: {nodeA.Label} - {nodeB.Label}");
                    }
                }
            }
        }
    }
}
