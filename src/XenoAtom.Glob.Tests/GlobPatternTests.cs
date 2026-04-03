// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Tests;

[TestClass]
public class GlobPatternTests
{
    [TestMethod]
    [DataRow("file.txt", "file.txt", true)]
    [DataRow("file.txt", "other.txt", false)]
    [DataRow("src/file.txt", "src/file.txt", true)]
    [DataRow("src/file.txt", "src/other.txt", false)]
    [DataRow("*.txt", "file.txt", true)]
    [DataRow("*.txt", "src/file.txt", false)]
    [DataRow("file?.txt", "file1.txt", true)]
    [DataRow("file?.txt", "file10.txt", false)]
    [DataRow("file[0-9].txt", "file5.txt", true)]
    [DataRow("file[!0-9].txt", "filex.txt", true)]
    [DataRow("file[!0-9].txt", "file5.txt", false)]
    [DataRow(@"file\*.txt", "file*.txt", true)]
    [DataRow("**", "src/nested/file.txt", true)]
    [DataRow("**/file.txt", "file.txt", true)]
    [DataRow("**/file.txt", "src/file.txt", true)]
    [DataRow("src/**", "src/file.txt", true)]
    [DataRow("src/**", "src/nested/file.txt", true)]
    [DataRow("src/**/file.txt", "src/file.txt", true)]
    [DataRow("src/**/file.txt", "src/nested/deep/file.txt", true)]
    [DataRow("src/**/file.txt", "other/file.txt", false)]
    [DataRow("prefix*", "prefix-value", true)]
    [DataRow("*.suffix", "value.suffix", true)]
    public void IsMatch_ShouldReturnExpectedResult(string patternText, string path, bool expected)
    {
        var pattern = GlobPattern.Parse(patternText);

        Assert.AreEqual(expected, pattern.IsMatch(path));
    }

    [TestMethod]
    public void IsMatch_ShouldNormalizeInputSeparators()
    {
        var pattern = GlobPattern.Parse("src/**/file.txt");

        Assert.IsTrue(pattern.IsMatch(@"src\nested\file.txt"));
    }

    [TestMethod]
    public void TryParse_ShouldRejectLeadingSeparator()
    {
        var result = GlobPattern.TryParse("/file.txt");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(GlobPatternParseError.LeadingSeparatorNotAllowed, result.Error);
    }

    [TestMethod]
    public void TryParse_ShouldRejectTrailingSeparator()
    {
        var result = GlobPattern.TryParse("src/");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(GlobPatternParseError.TrailingSeparatorNotAllowed, result.Error);
    }

    [TestMethod]
    public void TryParse_ShouldRejectTrailingEscape()
    {
        var result = GlobPattern.TryParse(@"file\");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(GlobPatternParseError.InvalidEscapeSequence, result.Error);
    }

    [TestMethod]
    public void TryParse_ShouldRejectUnterminatedCharacterClass()
    {
        var result = GlobPattern.TryParse("file[0-9");

        Assert.IsFalse(result.Success);
        Assert.AreEqual(GlobPatternParseError.UnterminatedCharacterClass, result.Error);
    }

    [TestMethod]
    public void Parse_ShouldThrowForInvalidPatterns()
    {
        var ex = Assert.Throws<ArgumentException>(() => GlobPattern.Parse("file[9-0].txt"));

        StringAssert.Contains(ex.Message, "invalid character class range", StringComparison.OrdinalIgnoreCase);
    }
}
