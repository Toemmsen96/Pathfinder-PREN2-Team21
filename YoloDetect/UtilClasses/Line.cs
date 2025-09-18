namespace YoloDetect{
           public class Line{
            public string Name { get; set; }
            public Coordinate Start { get; set; }
            public Coordinate End { get; set; }
            public Line(Coordinate start, Coordinate end)
            {
                Name = Guid.NewGuid().ToString();
                Start = start;
                End = end;
            }
            public Line(string name, Coordinate start, Coordinate end)
            {
                Name = name;
                Start = start;
                End = end;
            }
            public float Length()
            {
                return Coordinate.Distance(Start, End);
            }
            public float Angle()
            {
                return (float)Math.Atan2(End.Y - Start.Y, End.X - Start.X);
            }
            public float DistancePointToLine(Coordinate point)
            {
                // Calculate distance to Start point
                float distanceToStart = Coordinate.Distance(point, Start);
                
                // Calculate distance to End point
                float distanceToEnd = Coordinate.Distance(point, End);
                
                // Return the smaller of the two distances
                return Math.Min(distanceToStart, distanceToEnd);
            }
        }

}