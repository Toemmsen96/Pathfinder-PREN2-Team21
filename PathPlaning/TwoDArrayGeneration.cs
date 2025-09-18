using System;
using System.Collections.Generic;

namespace PathPlaning
{
public static class TwoDArrayGenerator
{
    public static List<(string, string)> GenerateEdges(double[,] matrix, Graph graph)
    {
        var nodeLabels = graph.Nodes
            .OrderBy(n => n.Id)
            .Select(n => n.Label)
            .ToArray();

        var edgesList = new List<(string, string)>();
        int size = matrix.GetLength(0);

        for (int i = 0; i < size; i++)
        {
            for (int j = i + 1; j < size; j++)
            {
                if (matrix[i, j] > 0)
                {
                    edgesList.Add((nodeLabels[i], nodeLabels[j]));
                }
            }
        }

        return edgesList;
    }
}
}