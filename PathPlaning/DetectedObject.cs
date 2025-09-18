// Neue Modellstruktur für standard.json – getrennte Klassen für Nodes, Lines, Pylons
using System;

namespace PathPlaning
{
    public class NodeObject
    {
        public string class_name { get; set; } = "Node";
        public double x { get; set; }
        public double y { get; set; }
        public string detection_id { get; set; } = string.Empty;

        public (double X, double Y) Center => (x, y);
    }

    public class PylonObject
    {
        public string class_name { get; set; } = "Pylon";
        public double x { get; set; }
        public double y { get; set; }
        public string detection_id { get; set; } = string.Empty;

        public (double X, double Y) Center => (x, y);
    }

    public class LineObject
    {
        public string class_name { get; set; } = "Line";
        public double xStart { get; set; }
        public double yStart { get; set; }
        public double xEnd { get; set; }
        public double yEnd { get; set; }
        public string detection_id { get; set; } = string.Empty;

        public (double x1, double y1, double x2, double y2) GetEndpoints() => (xStart, yStart, xEnd, yEnd);
    }

    public class ModelResult
    {
        public int image_width = 0;

        public int image_height = 0;

        public List<NodeObject> Nodes { get; set; } = new();
        public List<PylonObject> Pylons { get; set; } = new();
        public List<LineObject> Lines { get; set; } = new();
    }
}
