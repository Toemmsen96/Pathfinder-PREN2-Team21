use PathPlaning.dll as dependency

Usage:

using PathPlaning;
var path = PathPlanner.ComputePath("test_detection.json", "Start", "A"); // StartNode should be fix Start
Console.WriteLine(string.Join(" -> ", path));