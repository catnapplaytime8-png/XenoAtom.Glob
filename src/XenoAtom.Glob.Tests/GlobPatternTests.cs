// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Internal;

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
    public void IsMatch_ShouldHandleEmptyPattern()
    {
        var pattern = GlobPattern.Parse(string.Empty);

        Assert.IsTrue(pattern.IsMatch(string.Empty));
        Assert.IsFalse(pattern.IsMatch("file.txt"));
    }

    [TestMethod]
    public void IsMatch_ShouldHandleBareStar()
    {
        var pattern = GlobPattern.Parse("*");

        Assert.IsTrue(pattern.IsMatch("file.txt"));
        Assert.IsFalse(pattern.IsMatch("src/file.txt"));
        Assert.IsFalse(pattern.IsMatch(string.Empty));
    }

    [TestMethod]
    public void IsMatch_ShouldCollapseConsecutiveRecursiveWildcards()
    {
        var pattern = GlobPattern.Parse("src/**/**/file.txt");

        Assert.IsTrue(pattern.IsMatch("src/file.txt"));
        Assert.IsTrue(pattern.IsMatch("src/nested/deep/file.txt"));
        Assert.IsFalse(pattern.IsMatch("other/file.txt"));
    }

    [TestMethod]
    public void IsMatch_ShouldHonorEscapedCharacterClassLiterals()
    {
        Assert.IsTrue(GlobPattern.Parse(@"file[\]].txt").IsMatch("file].txt"));
        Assert.IsFalse(GlobPattern.Parse(@"file[\]].txt").IsMatch("filea.txt"));
        Assert.IsTrue(GlobPattern.Parse(@"file[\-].txt").IsMatch("file-.txt"));
        Assert.IsFalse(GlobPattern.Parse(@"file[\-].txt").IsMatch("filea.txt"));
    }

    [TestMethod]
    public void IsMatch_ShouldBacktrackAcrossSingleSegmentStars()
    {
        Assert.IsTrue(GlobPattern.Parse("*test*").IsMatch("mytestfile"));
        Assert.IsFalse(GlobPattern.Parse("*test*").IsMatch("mytoastfile"));

        Assert.IsTrue(GlobPattern.Parse("a*b*c").IsMatch("axbyc"));
        Assert.IsTrue(GlobPattern.Parse("a*b*c").IsMatch("abc"));
        Assert.IsFalse(GlobPattern.Parse("a*b*c").IsMatch("acb"));
    }

    [TestMethod]
    public void IsMatch_ShouldKeepLeadingLiteralAnchoredBeforeFirstStar()
    {
        Assert.IsFalse(GlobPattern.Parse("unittest*.pb.*").IsMatch("message_differencer_unittest.pb.obj"));
        Assert.IsFalse(GlobPattern.Parse("coverage.*[.json, .xml, .info]").IsMatch("AssemblyCoverage.cs"));
    }

    [TestMethod]
    public void IsMatch_ShouldHandleSingleEntryAndLiteralOpeningBracketCharacterClasses()
    {
        Assert.IsTrue(GlobPattern.Parse("file[a].txt").IsMatch("filea.txt"));
        Assert.IsFalse(GlobPattern.Parse("file[a].txt").IsMatch("fileb.txt"));

        Assert.IsTrue(GlobPattern.Parse("[[]").IsMatch("["));
        Assert.IsFalse(GlobPattern.Parse("[[]").IsMatch("a"));
    }

    [TestMethod]
    public void Match_ShouldFoldCharacterClassesWhenComparisonIsIgnoreCase()
    {
        var upper = GlobParser.TryParse("[A-Z].txt", GlobParserOptions.IgnorePattern);
        var lower = GlobParser.TryParse("[a-z].cs", GlobParserOptions.IgnorePattern);
        var negated = GlobParser.TryParse("[!A-Z].bin", GlobParserOptions.IgnorePattern);

        Assert.IsTrue(upper.Success);
        Assert.IsTrue(lower.Success);
        Assert.IsTrue(negated.Success);

        Assert.IsTrue(upper.Pattern.Match(PathNormalizer.NormalizeRelativePath("a.txt"), PathStringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(lower.Pattern.Match(PathNormalizer.NormalizeRelativePath("A.cs"), PathStringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(negated.Pattern.Match(PathNormalizer.NormalizeRelativePath("a.bin"), PathStringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(negated.Pattern.Match(PathNormalizer.NormalizeRelativePath("1.bin"), PathStringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void Match_ShouldKeepLeadingLiteralAnchoredBeforeFirstStar_WhenIgnoreCase()
    {
        var parseResult = GlobParser.TryParse("coverage.*[.json, .xml, .info]", GlobParserOptions.IgnorePattern);
        Assert.IsTrue(parseResult.Success);

        Assert.IsFalse(parseResult.Pattern.Match(
            PathNormalizer.NormalizeRelativePath("AssemblyCoverage.cs"),
            PathStringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(parseResult.Pattern.Match(
            PathNormalizer.NormalizeRelativePath("ReleaserApp.Coverage.cs"),
            PathStringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void IsMatch_ShouldNormalizeInputSeparators()
    {
        var pattern = GlobPattern.Parse("src/**/file.txt");

        Assert.IsTrue(pattern.IsMatch(@"src\nested\file.txt"));
    }

    [TestMethod]
    public void IsMatch_ShouldSupportReadOnlySpanOverload()
    {
        var pattern = GlobPattern.Parse("src/**/file.txt");
        ReadOnlySpan<char> candidate = @"src\nested\file.txt";

        Assert.IsTrue(pattern.IsMatch(candidate));
    }

    [TestMethod]
    public void IsMatch_ShouldSupportUnicodePaths()
    {
        var pattern = GlobPattern.Parse("donn\u00E9es/**/\u00E9t\u00E9.txt");

        Assert.IsTrue(pattern.IsMatch("donn\u00E9es/archives/\u00E9t\u00E9.txt"));
        Assert.IsFalse(pattern.IsMatch("donnees/archives/ete.txt"));
    }

    [TestMethod]
    public void IsMatch_ShouldSupportLongReadOnlySpanPaths()
    {
        var pattern = GlobPattern.Parse("**/target.txt");
        ReadOnlySpan<char> candidate = CreateLongRelativePath(segmentCount: 36, segmentLength: 8, leafName: "target.txt");

        Assert.IsTrue(pattern.IsMatch(candidate));
    }

    [TestMethod]
    public void IsMatch_ShouldTreatExplicitDirectoryFlagLikeTrailingSeparator()
    {
        var pattern = GlobPattern.Parse("src");

        Assert.IsTrue(pattern.IsMatch("src", isDirectory: true));
        Assert.IsTrue(pattern.IsMatch("src/"));
        Assert.IsFalse(pattern.IsMatch("src/nested", isDirectory: true));
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

    private static string CreateLongRelativePath(int segmentCount, int segmentLength, string leafName)
    {
        var segments = Enumerable.Repeat(new string('a', segmentLength), segmentCount);
        return string.Join('/', segments.Append(leafName));
    }
}
