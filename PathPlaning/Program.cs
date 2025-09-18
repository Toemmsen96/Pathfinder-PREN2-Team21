// Program.cs – aktualisiert für korrektes Mapping
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace PathPlaning
{
    class Program
    {
        static void Main(string[] args)
        {
            string jsonPath;
            
            if (args.Length > 0)
            {
                // If an argument is provided, use it as the JSON file path
                jsonPath = args[0];
                
                // If it's just a filename, check both current directory and images folder
                if (!jsonPath.Contains("/") && !jsonPath.Contains("\\"))
                {
                    string currentDirPath = Path.Combine(Directory.GetCurrentDirectory(), jsonPath);
                    string imagesDirPath = Path.Combine(Directory.GetCurrentDirectory() + "/images", jsonPath);
                    
                    if (File.Exists(currentDirPath))
                        jsonPath = currentDirPath;
                    else if (File.Exists(imagesDirPath))
                        jsonPath = imagesDirPath;
                    else
                        jsonPath = currentDirPath; // Will fail later with proper error message
                }
            }
            else
            {
                // Default to test123.json if no argument provided
                jsonPath = Path.Combine(Directory.GetCurrentDirectory() + "/images", "test123.json");
            }
            
            Console.WriteLine(jsonPath);
            if (!File.Exists(jsonPath))
            {
                Console.WriteLine($"Fehler: {jsonPath} nicht gefunden.");
                return;
            }

            string jsonString = File.ReadAllText(jsonPath);
            var model = JsonSerializer.Deserialize<ModelResult>(jsonString);
            if (model == null)
            {
                Console.WriteLine("JSON konnte nicht korrekt deserealisiert werden.");
                return;
            }

            // Build complete graph including all detected lines (for line selection)
            Graph completeGraph = AdjacencyMatrixBuilder.BuildCompleteGraph(model);
            
            // Build traversable graph excluding blocked node connections (for pathfinding)
            Graph traversableGraph = AdjacencyMatrixBuilder.BuildTraversableGraph(model);
            
            Console.WriteLine("\nComplete Graph (all detected lines):");
            Console.WriteLine("Erkannte Verbindungen:");
            foreach (var edge in completeGraph.Edges)
            {
                Console.WriteLine($"{edge.A.Label}  {edge.A.IsBlocked} ↔ {edge.B.Label}   {edge.B.IsBlocked}");
            }
            
            Console.WriteLine("\nTraversable Graph (pathfinding only):");
            Console.WriteLine("Traversable Verbindungen:");
            foreach (var edge in traversableGraph.Edges)
            {
                Console.WriteLine($"{edge.A.Label}  {edge.A.IsBlocked} ↔ {edge.B.Label}   {edge.B.IsBlocked}");
            }
            
            double[,] matrix = AdjacencyMatrixBuilder.BuildWeightedMatrix(traversableGraph);

            Console.WriteLine("Adjazenzmatrix:\n");
            int size = matrix.GetLength(0);

            // Matrix drucken mit Labels (from traversable graph)
            var labels = traversableGraph.Nodes.Select(n => n.Label).ToList();

            // Kopfzeile
            Console.Write("     ");
            foreach (var label in labels)
                Console.Write($"{label,6}");
            Console.WriteLine();

            // Zeilen
            for (int i = 0; i < labels.Count; i++)
            {
                Console.Write($"{labels[i],5}");
                for (int j = 0; j < labels.Count; j++)
                {
                    Console.Write($"{matrix[i, j],6}");
                }
                Console.WriteLine();
            }


            Console.WriteLine("\nTraversable Verbindungen (for pathfinding):");
            foreach (var edge in traversableGraph.Edges)
            {
                Console.WriteLine($"{edge.A.Label} - {edge.B.Label}");
            }

            // Use COMPLETE graph for line selection indices (includes all detected lines)
            // But use TRAVERSABLE graph for pathfinding (excludes blocked connections)
            
            // Tatsächliche Kanten (from traversable graph for pathfinding)
            var actual = new HashSet<(string, string)>(
            traversableGraph.Edges.Select(e => (e.A.Label, e.B.Label))
                 .Select(p => string.Compare(p.Item1, p.Item2) < 0 ? p : (p.Item2, p.Item1))
            );

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

            string startNode = "Start";
            string endNode = "C";

            var path = AStar.FindPath(astarGraph, startNode, endNode, AStar.HeuristicsNew[endNode]);
            Console.WriteLine("Fastest Path:");
            Console.WriteLine(string.Join(" -> ", path));

            List<int> calculatedLineNumbersPerNode = PathInterpreter.GetLineSelectionIndices(completeEdgeList, path);
            Console.WriteLine("Calculated Line Numbers per Node:");
            Console.WriteLine(string.Join(" -> ", calculatedLineNumbersPerNode));

            Console.WriteLine("\nTesting PathPlaner.ComputePath method (DLL interface):");
            var dllResult = PathPlaner.ComputePath(jsonPath, startNode, endNode, false);
            Console.WriteLine($"DLL Result: {string.Join(" -> ", dllResult)}");
            
            if (calculatedLineNumbersPerNode.SequenceEqual(dllResult))
            {
                Console.WriteLine("✅ DLL and main program return identical results!");
            }
            else
            {
                Console.WriteLine("❌ DLL and main program return different results!");
            }

            GraphVisualizer.DrawGraph(completeGraph);
        }
    }
}