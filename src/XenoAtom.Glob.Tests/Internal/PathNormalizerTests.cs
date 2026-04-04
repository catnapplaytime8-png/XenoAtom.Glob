// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.Tests.Internal;

[TestClass]
public class PathNormalizerTests
{
    [TestMethod]
    [DataRow("src/file.txt", false, "src/file.txt", false)]
    [DataRow(@"src\file.txt", false, "src/file.txt", false)]
    [DataRow(@"src\\nested///file.txt", false, "src/nested/file.txt", false)]
    [DataRow("./src/./file.txt", false, "src/file.txt", false)]
    [DataRow("src/folder/", false, "src/folder", true)]
    [DataRow("src/folder", true, "src/folder", true)]
    [DataRow(".", false, "", false)]
    [DataRow("./", false, "", true)]
    [DataRow("", false, "", false)]
    public void NormalizeRelativePath_ShouldNormalizeExpectedValue(string path, bool isDirectory, string expectedValue, bool expectedDirectory)
    {
        var normalized = PathNormalizer.NormalizeRelativePath(path, isDirectory);

        Assert.AreEqual(expectedValue, normalized.Value);
        Assert.AreEqual(expectedDirectory, normalized.IsDirectory);
    }

    [TestMethod]
    [DataRow("/src/file.txt", (int)PathNormalizationError.AbsolutePathNotSupported)]
    [DataRow(@"\src\file.txt", (int)PathNormalizationError.AbsolutePathNotSupported)]
    [DataRow(@"C:\src\file.txt", (int)PathNormalizationError.AbsolutePathNotSupported)]
    [DataRow("../file.txt", (int)PathNormalizationError.ParentDirectorySegmentsNotSupported)]
    [DataRow("src/../file.txt", (int)PathNormalizationError.ParentDirectorySegmentsNotSupported)]
    public void TryNormalizeRelativePath_ShouldRejectUnsupportedPaths(string path, int expectedError)
    {
        var result = PathNormalizer.TryNormalizeRelativePath(path);

        Assert.IsFalse(result.Success);
        Assert.AreEqual((PathNormalizationError)expectedError, result.Error);
    }

    [TestMethod]
    public void NormalizeRelativePath_ShouldThrowForUnsupportedPaths()
    {
        var ex = Assert.Throws<ArgumentException>(() => PathNormalizer.NormalizeRelativePath("../file.txt"));

        StringAssert.Contains(ex.Message, "Parent directory segments");
    }

    [TestMethod]
    public void SegmentEnumerator_ShouldReturnNormalizedSegments()
    {
        var normalized = PathNormalizer.NormalizeRelativePath("src/nested/file.txt");
        var segments = new List<string>();
        foreach (var segment in normalized.EnumerateSegments())
        {
            segments.Add(segment.ToString());
        }

        CollectionAssert.AreEqual(new[] { "src", "nested", "file.txt" }, segments);
    }

    [TestMethod]
    public void SegmentCount_ShouldCountSegments()
    {
        var normalized = PathNormalizer.NormalizeRelativePath("src/nested/file.txt");

        Assert.AreEqual(3, normalized.SegmentCount);
    }

    [TestMethod]
    [DataRow("src/nested/file.txt", false, 3, false)]
    [DataRow(@"src\nested\file.txt", false, 3, false)]
    [DataRow("./src/./nested/file.txt", false, 3, false)]
    [DataRow("src/folder/", false, 2, true)]
    [DataRow("", false, 0, false)]
    public void SegmentCount_ShouldBePreservedAcrossFastAndSlowNormalizationPaths(string path, bool isDirectory, int expectedSegmentCount, bool expectedDirectory)
    {
        var normalized = PathNormalizer.NormalizeRelativePath(path, isDirectory);

        Assert.AreEqual(expectedSegmentCount, normalized.SegmentCount);
        Assert.AreEqual(expectedDirectory, normalized.IsDirectory);
    }

    [TestMethod]
    [DataRow("src/file.txt")]
    [DataRow("src/folder/")]
    [DataRow(@"src\folder\child.txt")]
    [DataRow("./src/file.txt")]
    [DataRow("a//b///c")]
    public void NormalizeRelativePath_ShouldBeIdempotent(string path)
    {
        var first = PathNormalizer.NormalizeRelativePath(path);
        var second = PathNormalizer.NormalizeRelativePath(first.Value, first.IsDirectory);

        Assert.AreEqual(first, second);
    }
}
