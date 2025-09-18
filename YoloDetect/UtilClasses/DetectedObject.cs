namespace YoloDetect
{
    public class DetectedObject{
        public int ClassId { get; set; }
        public double Confidence { get; set; }
        public BoundingBox BoundingBox { get; set; }
        public string ClassName { get; set; }
        public string DetectionId { get; set; }

        public DetectedObject(int classId, double confidence, BoundingBox boundingBox, string className, string detectionId)
        {
            ClassId = classId;
            Confidence = confidence;
            BoundingBox = boundingBox;
            ClassName = className;
            DetectionId = detectionId;
        }
    }
    public class BoundingBox
    {
        public Coordinate TopLeft { get; set; }
        public Coordinate TopRight { get; set; }
        public Coordinate BottomLeft { get; set; }
        public Coordinate BottomRight { get; set; }

        public BoundingBox(Coordinate topLeft, Coordinate topRight, Coordinate bottomLeft, Coordinate bottomRight)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomLeft = bottomLeft;
            BottomRight = bottomRight;
        }

        public Coordinate GetCenter()
        {
            return new Coordinate(
                (TopLeft.X + BottomRight.X) / 2,
                (TopLeft.Y + BottomRight.Y) / 2
            );
        }

        // This method calculates the lower center of the bounding box (mainly for pylons)
        public Coordinate GetLowerCenter() 
        {
            return new Coordinate(
                (BottomLeft.X + BottomRight.X) / 2,
                BottomLeft.Y + (TopLeft.Y - BottomLeft.Y) / 7
            );
        }
    }
}