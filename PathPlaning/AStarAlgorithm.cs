using System;
using System.Collections.Generic;
using System.Linq;

public class AStar
{
    public static Dictionary<string, Dictionary<string, double>> HeuristicsNew = new()
    {
        ["A"] = new Dictionary<string, double> { ["Start"] = 2, ["N1"] = 1, ["N2"] = 1, ["N3"] = 2, ["N4"] = 1, ["A"] = 0, ["B"] = 1, ["C"] = 2 },
        ["B"] = new Dictionary<string, double> { ["Start"] = 3, ["N1"] = 2, ["N2"] = 2, ["N3"] = 2, ["N4"] = 1, ["A"] = 1, ["B"] = 0, ["C"] = 1 },
        ["C"] = new Dictionary<string, double> { ["Start"] = 2, ["N1"] = 3, ["N2"] = 2, ["N3"] = 1, ["N4"] = 1, ["A"] = 2, ["B"] = 1, ["C"] = 0 }
    };

    public static List<string> FindPath(AStarGraph graph, string start, string goal, Dictionary<string, double> heuristic)
    {
        // Debug: Check if start and goal nodes exist
        if (!graph.Nodes.Contains(start))
        {
            Console.WriteLine($"Error: Start node '{start}' not found in graph");
            return new List<string>();
        }
        if (!graph.Nodes.Contains(goal))
        {
            Console.WriteLine($"Error: Goal node '{goal}' not found in graph");
            return new List<string>();
        }

        // Debug: Show available nodes and connections
        Console.WriteLine($"A* Pathfinding from {start} to {goal}");
        Console.WriteLine($"Available nodes: {string.Join(", ", graph.Nodes)}");
        Console.WriteLine($"Start neighbors: {string.Join(", ", graph.GetNeighbors(start))}");
        Console.WriteLine($"Goal neighbors: {string.Join(", ", graph.GetNeighbors(goal))}");

        var openSet = new HashSet<string> { start };
        var cameFrom = new Dictionary<string, string>();
        var gScore = graph.Nodes.ToDictionary(node => node, node => double.PositiveInfinity);
        var fScore = graph.Nodes.ToDictionary(node => node, node => double.PositiveInfinity);
        gScore[start] = 0;
        fScore[start] = heuristic.ContainsKey(start) ? heuristic[start] : double.PositiveInfinity;

        while (openSet.Any())
        {
            var current = openSet.OrderBy(n => fScore[n]).First();
            if (current == goal)
            {
                var path = new List<string>();
                while (cameFrom.ContainsKey(current))
                {
                    path.Insert(0, current);
                    current = cameFrom[current];
                }
                path.Insert(0, start);
                Console.WriteLine($"Path found: {string.Join(" -> ", path)}");
                return path;
            }

            openSet.Remove(current);
            foreach (var neighbor in graph.GetNeighbors(current))
            {
                double tentativeGScore = gScore[current] + 1; // Unweighted graph
                if (tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + (heuristic.ContainsKey(neighbor) ? heuristic[neighbor] : double.PositiveInfinity);
                    openSet.Add(neighbor);
                }
            }
        }

        Console.WriteLine($"No path found from {start} to {goal}");
        return new List<string>(); // No path found
    }
}

public class Graph
{
    private Dictionary<string, List<string>> adjList = new();

    public IEnumerable<string> Nodes => adjList.Keys;

    public void AddEdge(string a, string b)
    {
        if (!adjList.ContainsKey(a)) adjList[a] = new List<string>();
        if (!adjList.ContainsKey(b)) adjList[b] = new List<string>();
        adjList[a].Add(b);
        adjList[b].Add(a);
    }

    public IEnumerable<string> GetNeighbors(string node)
    {
        return adjList.TryGetValue(node, out var neighbors) ? neighbors : Enumerable.Empty<string>();
    }
}
