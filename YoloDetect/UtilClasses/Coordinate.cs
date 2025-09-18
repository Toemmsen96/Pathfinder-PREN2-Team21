namespace YoloDetect{

        public class Coordinate{
            public float X { get; set; }
            public float Y { get; set; }

            public Coordinate(float x, float y)
            {
                X = x;
                Y = y;
            }

            public static float Distance(Coordinate cord1, Coordinate cord2)
            {
                return (float)Math.Sqrt(Math.Pow(cord1.X - cord2.X, 2) + Math.Pow(cord1.Y - cord2.Y, 2));
            }
        }
}