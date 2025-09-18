// PathPlaner.cs – angepasst für neue ModelResult mit Nodes, Pylons, Lines
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PathPlaning
{
    public static class PathPlaner
    {
        public static List<int> ComputePath(string jsonPath, string startNode, string endNode, bool withGraphPicture = false)
        {
            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("JSON-Datei nicht gefunden", jsonPath);

            string json = File.ReadAllText(jsonPath);
            var result = JsonSerializer.Deserialize<ModelResult>(json);
            if (result == null)
                throw new IOException("Konnte JSON nicht laden oder deserialisieren");

            // Build both complete and traversable graphs (same as Program.cs)
            var completeGraph = AdjacencyMatrixBuilder.BuildCompleteGraph(result);
            var traversableGraph = AdjacencyMatrixBuilder.BuildTraversableGraph(result);
            
            // Create edge list from COMPLETE graph for line selection (includes all detected lines)
            List<(string, string)> completeEdgeList = completeGraph.Edges
                .Select(e => (e.A.Label, e.B.Label))
                .ToList();

            var astarGraph = AStarGraph.FromGraph(traversableGraph);

            // Alle erlaubten Labels (nicht blockierte Nodes) - from traversable graph
            var allowedLabels = new HashSet<string>(
                traversableGraph.Nodes.Where(n => !n.IsBlocked).Select(n => n.Label)
            );

            // Filter the edge list to only include non-blocked nodes (use traversable edges for pathfinding)
            var filteredEdges = completeEdgeList
                .Where(edge => allowedLabels.Contains(edge.Item1) && allowedLabels.Contains(edge.Item2))
                .ToList();

            // Nur erlaubte Verbindungen hinzufügen
            foreach (var edge in filteredEdges)
            {
                astarGraph.AddEdge(edge.Item1, edge.Item2);
            }

            List<string> fastestPath = AStar.FindPath(astarGraph, startNode, endNode, AStar.HeuristicsNew[endNode]);

            Console.WriteLine("Fastest Path:");
            Console.WriteLine(string.Join(" -> ", fastestPath));

            // Use COMPLETE edge list for line selection (same as Program.cs)
            List<int> calculatedLineNumbersPerNode = PathInterpreter.GetLineSelectionIndices(completeEdgeList, fastestPath);

            Console.WriteLine("Calculated Line Numbers per Node:");
            Console.WriteLine(string.Join(" -> ", calculatedLineNumbersPerNode));

            if (withGraphPicture)
            {
                GraphVisualizer.DrawGraph(completeGraph);
            }

            return calculatedLineNumbersPerNode;
        }
    }
}