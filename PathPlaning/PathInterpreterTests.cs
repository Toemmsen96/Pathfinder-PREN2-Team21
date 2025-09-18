using NUnit.Framework;
using System.Collections.Generic;
using PathPlaning;

namespace PathPlaningTests
{
    [TestFixture]
    public class PathInterpreterTests
    {
        private static readonly List<(string, string)> EdgeList = new()
        {
            ("Start", "N1"), ("Start", "N3"), ("Start", "N2"),
            ("N1", "C"), ("N1", "N4"), ("N1", "N3"), ("N2", "A"),
            ("C", "B"), ("B", "N4"), ("A", "B"), ("C", "N4"),
            ("N4", "N3"), ("N3", "N2"), ("N3", "A"), ("N4", "A")
        };

        public static IEnumerable<TestCaseData> PathTestCases()
        {
            yield return new TestCaseData(new List<string> { "Start", "N1", "C" }, new List<int> { 1, 1 });
            yield return new TestCaseData(new List<string> { "Start", "N3", "N4" }, new List<int> { 2, 2 });
            yield return new TestCaseData(new List<string> { "Start", "N2", "A" }, new List<int> { 3, 2 });
            yield return new TestCaseData(new List<string> { "Start", "N3", "A" }, new List<int> { 2, 3 });
            yield return new TestCaseData(new List<string> { "Start", "N1", "N3" }, new List<int> { 1, 3 });
            yield return new TestCaseData(new List<string> { "Start", "N1", "N4", "B" }, new List<int> { 1, 2, 2 });
            yield return new TestCaseData(new List<string> { "Start", "N3", "N4", "B" }, new List<int> { 2, 2, 3 });
            yield return new TestCaseData(new List<string> { "Start", "N3", "A", "B" }, new List<int> { 2, 3, 2 });
            yield return new TestCaseData(new List<string> { "Start", "N2", "N3", "N4" }, new List<int> { 3, 1, 3 });
            yield return new TestCaseData(new List<string> { "Start", "N1", "N4", "A" }, new List<int> { 1, 2, 3 });
            yield return new TestCaseData(new List<string> { "Start", "N3", "N4", "A" }, new List<int> { 2, 2, 4 });
            yield return new TestCaseData(new List<string> { "Start", "N1", "C", "B" }, new List<int> { 1, 1, 1 });
            yield return new TestCaseData(new List<string> { "Start", "N3", "N2", "A" }, new List<int> { 2, 4, 1 });
            yield return new TestCaseData(new List<string> { "Start", "N1", "N3", "A" }, new List<int> { 1, 3, 2 });
            yield return new TestCaseData(new List<string> { "Start", "N3", "A", "B", "N4" }, new List<int> { 2, 3, 2, 1 });
            yield return new TestCaseData(new List<string> { "Start", "N2", "N3", "N1", "C" }, new List<int> { 3, 1, 2, 2 });
            yield return new TestCaseData(new List<string> { "Start", "N3", "N1", "C", "B" }, new List<int> { 2, 1, 2, 1 });
            yield return new TestCaseData(new List<string> { "Start", "N3", "N4", "N1", "C" }, new List<int> { 2, 2, 1, 3 });
            yield return new TestCaseData(new List<string> { "Start", "N2", "A", "N4", "C", "N1", "N3" }, new List<int> { 3, 2, 2, 3, 1, 2 });            
        }

        [Test, TestCaseSource(nameof(PathTestCases))]
        public void TestPathInterpreter(List<string> path, List<int> expected)
        {
            var result = PathInterpreter.GetLineSelectionIndices(EdgeList, path);
            NUnit.Framework.Legacy.CollectionAssert.AreEqual(expected, result,
            $"Pfad fehlgeschlagen: [{string.Join(" -> ", path)}] - Erwartet: [{string.Join(",", expected)}], Erhalten: [{string.Join(",", result)}]");
        }
    }
}
