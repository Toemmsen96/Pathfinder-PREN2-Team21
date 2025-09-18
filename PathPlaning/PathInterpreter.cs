using System;
using System.Collections.Generic;
using System.Linq;

namespace PathPlaning
{
    public static class PathInterpreter
    {
        // Feste, links-nach-rechts ausgerichtete Nachbarschaftsreihenfolge für jeden Node
        private static readonly Dictionary<string, List<string>> NeighborPriority = new()
        {
            ["Start"] = new() { "N1", "N3", "N2" },
            ["N1"] = new() { "Start", "C", "N4", "N3", "B" },
            ["N2"] = new() { "Start", "N3", "A" },
            ["N3"] = new() { "Start", "N1", "N4", "A", "N2", "B" },
            ["N4"] = new() { "N1", "C", "B", "A", "N3" },
            ["A"] = new() { "N3", "N4", "B", "N2" },
            ["B"] = new() { "C", "A", "N4", "N3", "N1" },
            ["C"] = new() { "N1", "B", "N4" },
        };

        public static List<int> GetLineSelectionIndices(List<(string, string)> edgeList, List<string> path)
        {
            var selection = new List<int>();

            for (int i = 0; i < path.Count - 1; i++)
            {
                string current = path[i];
                string next = path[i + 1];
                string? lastVisited = i > 0 ? path[i - 1] : null;

                // Alle tatsächlichen Nachbarn gemäß Edgelist
                var neighbors = edgeList
                    .Where(e => e.Item1 == current || e.Item2 == current)
                    .Select(e => e.Item1 == current ? e.Item2 : e.Item1)
                    .Distinct()
                    .ToList();

                if (!NeighborPriority.TryGetValue(current, out var priorityList))
                    throw new Exception($"Kein Nachbarschaftsprofil für {current}");

                // Rotierte Nachbarschaftsliste
                List<string> rotated;
                if (lastVisited != null && priorityList.Contains(lastVisited))
                {
                    int idx = priorityList.IndexOf(lastVisited);
                    rotated = priorityList.Skip(idx + 1).Concat(priorityList.Take(idx + 1)).ToList();
                }
                else
                {
                    rotated = new List<string>(priorityList);
                }

                // Nur gültige Nachbarn in rotierter Reihenfolge
                var orderedConnections = rotated.Where(neighbors.Contains).ToList();

                int indexOfNext = orderedConnections.IndexOf(next);
                if (indexOfNext == -1)
                {
                    throw new Exception($"Node '{next}' ist kein gültiger Nachbar von '{current}'. " +
                                      $"Verfügbare Nachbarn: [{string.Join(", ", orderedConnections)}]");
                }
                
                int index = indexOfNext + 1;
                selection.Add(index);
            }

            return selection;
        }
    }
}