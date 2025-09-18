using System.Text.Json;

namespace YoloDetect
{
    public class Json{
        public static void ExportFinalJson(List<DetectedObject> nodesAndPylons,List<Line> drawnLines, string outputPath, int imageWidth, int imageHeight)
        {
            var outputStructure = new
            {
                image_width = imageWidth,
                image_height = imageHeight,
                Lines = drawnLines
                    .Select(line => new
                    {
                        xStart = line.Start.X,
                        yStart = line.Start.Y,
                        xEnd = line.End.X,
                        yEnd = line.End.Y,
                        class_name = "Line",
                        detection_id = line.GetHashCode().ToString()
                    }).ToList(),

                Nodes = nodesAndPylons
                    .Where(obj => obj.ClassName.ToLower() == "node")
                    .Select(node => new
                    {
                        x = node.BoundingBox.BottomRight.X,
                        y = node.BoundingBox.BottomRight.Y,
                        class_name = "Node",
                        detection_id = node.DetectionId
                    }).ToList(),

                Pylons = nodesAndPylons
                    .Where(obj => obj.ClassName.ToLower() == "traffic_cone" || obj.ClassName.ToLower() == "pylon" || obj.ClassName.ToLower() == "pylons")
                    .Select(pylon => new
                    {
                        x = pylon.BoundingBox.BottomRight.X,
                        y = pylon.BoundingBox.BottomRight.Y,
                        class_name = "Pylon",
                        detection_id = pylon.DetectionId
                    }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string jsonString = JsonSerializer.Serialize(outputStructure, options);
            File.WriteAllText(outputPath, jsonString);
        }
    }
}