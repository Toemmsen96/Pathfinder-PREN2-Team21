// GraphVisualizer.cs – zeichnet den Graph als PNG-Datei mit SkiaSharp
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PathPlaning
{
    public static class GraphVisualizer
    {
        public static void DrawGraph(Graph graph, string filePath = "graph.png")
        {
            int width = 1000;
            int height = 1000;
            int radius = 12;
            int margin = 50;

            using var bitmap = new SKBitmap(width, height);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            var paintEdge = new SKPaint
            {
                Color = SKColors.Black,
                StrokeWidth = 2,
                IsAntialias = true
            };

            var paintBlockedEdge = new SKPaint
            {
                Color = SKColors.LightGray,
                StrokeWidth = 2,
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash(new float[] { 10, 5 }, 0) // Dashed line for blocked edges
            };

            var paintNode = new SKPaint
            {
                Color = SKColors.Blue,
                IsAntialias = true
            };

            var paintBlockedNode = new SKPaint
            {
                Color = SKColors.Red,
                IsAntialias = true
            };

            var paintBlockedNodeBorder = new SKPaint
            {
                Color = SKColors.DarkRed,
                StrokeWidth = 3,
                Style = SKPaintStyle.Stroke,
                IsAntialias = true
            };

            var paintText = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true
            };

            var font = new SKFont
            {
                Size = 18
            };

            // Koordinaten normalisieren
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;
            foreach (var n in graph.Nodes)
            {
                minX = Math.Min(minX, n.Position.X);
                maxX = Math.Max(maxX, n.Position.X);
                minY = Math.Min(minY, n.Position.Y);
                maxY = Math.Max(maxY, n.Position.Y);
            }

            double scaleX = (width - 2 * margin) / (maxX - minX);
            double scaleY = (height - 2 * margin) / (maxY - minY);

            Dictionary<string, SKPoint> positions = new();
            foreach (var node in graph.Nodes)
            {
                float x = (float)((node.Position.X - minX) * scaleX + margin);
                float y = (float)((node.Position.Y - minY) * scaleY + margin);
                positions[node.Label] = new SKPoint(x, y);
            }

            // Kanten zeichnen
            foreach (var edge in graph.Edges)
            {
                var a = positions[edge.A.Label];
                var b = positions[edge.B.Label];
                if (edge.A.IsBlocked || edge.B.IsBlocked)
                {
                    canvas.DrawLine(a, b, paintBlockedEdge); // Use blocked edge paint for connections involving blocked nodes
                }
                else
                {
                    canvas.DrawLine(a, b, paintEdge);
                }
            }

            // Knoten zeichnen
            foreach (var node in graph.Nodes)
            {
                var pos = positions[node.Label];
                
                if (node.IsBlocked)
                {
                    // Draw blocked nodes in red with a darker red border
                    canvas.DrawCircle(pos, radius, paintBlockedNode);
                    canvas.DrawCircle(pos, radius, paintBlockedNodeBorder);
                    
                    // Add an X mark over blocked nodes
                    var paintX = new SKPaint
                    {
                        Color = SKColors.White,
                        StrokeWidth = 3,
                        IsAntialias = true
                    };
                    float xSize = radius * 0.7f;
                    canvas.DrawLine(pos.X - xSize, pos.Y - xSize, pos.X + xSize, pos.Y + xSize, paintX);
                    canvas.DrawLine(pos.X - xSize, pos.Y + xSize, pos.X + xSize, pos.Y - xSize, paintX);
                }
                else
                {
                    // Draw normal nodes in blue
                    canvas.DrawCircle(pos, radius, paintNode);
                }
                
                canvas.DrawText(node.Label, pos.X + radius + 2, pos.Y - 4, SKTextAlign.Left, font, paintText);
            }

            // Add title
            var titleFont = new SKFont { Size = 24 };
            var titlePaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            canvas.DrawText("Path Planning Graph Visualization", width / 2, 30, SKTextAlign.Center, titleFont, titlePaint);
            
            // Add subtitle with blocked node count
            var subtitleFont = new SKFont { Size = 16 };
            var blockedCount = graph.Nodes.Count(n => n.IsBlocked);
            string subtitle = $"Total Nodes: {graph.Nodes.Count}, Blocked: {blockedCount}, Edges: {graph.Edges.Count}";
            canvas.DrawText(subtitle, width / 2, 55, SKTextAlign.Center, subtitleFont, titlePaint);

            // Add legend
            DrawLegend(canvas, width, height, radius, paintNode, paintBlockedNode, font, paintText);

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.OpenWrite(filePath);
            data.SaveTo(stream);
        }

        private static void DrawLegend(SKCanvas canvas, int width, int height, int radius, 
                                      SKPaint paintNode, SKPaint paintBlockedNode, SKFont font, SKPaint paintText)
        {
            int legendX = 20;
            int legendY = height - 120;
            
            // Legend title
            canvas.DrawText("Legend:", legendX, legendY - 20, SKTextAlign.Left, font, paintText);
            
            // Normal node legend
            canvas.DrawCircle(legendX, legendY, radius, paintNode);
            canvas.DrawText("Normal Node", legendX + radius + 10, legendY + 5, SKTextAlign.Left, font, paintText);
            
            // Blocked node legend
            legendY += 30;
            canvas.DrawCircle(legendX, legendY, radius, paintBlockedNode);
            
            // Add X mark for blocked node legend
            var paintX = new SKPaint
            {
                Color = SKColors.White,
                StrokeWidth = 3,
                IsAntialias = true
            };
            float xSize = radius * 0.7f;
            canvas.DrawLine(legendX - xSize, legendY - xSize, legendX + xSize, legendY + xSize, paintX);
            canvas.DrawLine(legendX - xSize, legendY + xSize, legendX + xSize, legendY - xSize, paintX);
            
            canvas.DrawText("Blocked Node (Pylon)", legendX + radius + 10, legendY + 5, SKTextAlign.Left, font, paintText);
            
            // Edge legends
            legendY += 30;
            var paintNormalEdge = new SKPaint { Color = SKColors.Black, StrokeWidth = 2, IsAntialias = true };
            canvas.DrawLine(legendX - 10, legendY, legendX + 10, legendY, paintNormalEdge);
            canvas.DrawText("Normal Connection", legendX + 20, legendY + 5, SKTextAlign.Left, font, paintText);
            
            legendY += 20;
            var paintDashedEdge = new SKPaint 
            { 
                Color = SKColors.LightGray, 
                StrokeWidth = 2, 
                IsAntialias = true,
                PathEffect = SKPathEffect.CreateDash(new float[] { 10, 5 }, 0)
            };
            canvas.DrawLine(legendX - 10, legendY, legendX + 10, legendY, paintDashedEdge);
            canvas.DrawText("Connection to Blocked Node", legendX + 20, legendY + 5, SKTextAlign.Left, font, paintText);
        }
    }
}