// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.Tests.Internal;

[TestClass]
public class PropertyTests
{
    [TestMethod]
    public void PathNormalization_ShouldBeIdempotentForGeneratedRelativePaths()
    {
        var random = new Random(17);
        for (var i = 0; i < 250; i++)
        {
            var candidate = GenerateRelativePath(random);
            var normalized = PathNormalizer.NormalizeRelativePath(candidate.path, candidate.isDirectory);
            var normalizedAgain = PathNormalizer.NormalizeRelativePath(normalized.Value, normalized.IsDirectory);
            Assert.AreEqual(normalized, normalizedAgain, $"Candidate: {candidate.path}");
        }
    }

    [TestMethod]
    public void SpecializedAndGeneralMatchers_ShouldAgreeForGeneratedInputs()
    {
        var random = new Random(23);
        for (var i = 0; i < 250; i++)
        {
            var patternText = GeneratePattern(random);
            var parseResult = GlobParser.TryParse(patternText, GlobParserOptions.Default);
            Assert.IsTrue(parseResult.Success, $"Pattern should parse: {patternText}");

            var pathCandidate = GenerateRelativePath(random);
            var normalizedPath = PathNormalizer.NormalizeRelativePath(pathCandidate.path, pathCandidate.isDirectory);
            var optimized = parseResult.Pattern.Match(normalizedPath, PathStringComparison.Ordinal);
            var general = parseResult.Pattern.MatchGeneralOnly(normalizedPath, PathStringComparison.Ordinal);
            Assert.AreEqual(general, optimized, $"Pattern: {patternText}; Debug: {parseResult.Pattern.GetDebugView()}; Path: {normalizedPath}");
        }
    }

    [TestMethod]
    public void GlobParser_TryParse_ShouldNotThrowForGeneratedInputs()
    {
        var random = new Random(31);
        for (var i = 0; i < 500; i++)
        {
            var candidate = GenerateArbitraryGlob(random);
            _ = GlobPattern.TryParse(candidate);
        }
    }

    private static (string path, bool isDirectory) GenerateRelativePath(Random random)
    {
        var segmentCount = random.Next(0, 5);
        var builder = new StringBuilder();
        for (var i = 0; i < segmentCount; i++)
        {
            if (i > 0)
            {
                builder.Append(random.Next(2) == 0 ? '/' : '\\');
                if (random.Next(4) == 0)
                {
                    builder.Append(random.Next(2) == 0 ? '/' : '\\');
                }
            }

            if (random.Next(8) == 0)
            {
                builder.Append('.');
                continue;
            }

            var length = random.Next(1, 8);
            for (var j = 0; j < length; j++)
            {
                builder.Append((char)('a' + random.Next(0, 26)));
            }
        }

        var isDirectory = random.Next(2) == 0;
        if (isDirectory && builder.Length > 0 && builder[^1] is not '/' and not '\\')
        {
            builder.Append(random.Next(2) == 0 ? '/' : '\\');
        }

        return (builder.ToString(), isDirectory);
    }

    private static string GeneratePattern(Random random)
    {
        string[] patterns =
        [
            "file.txt",
            "*.txt",
            "prefix*",
            "*suffix",
            "src/**/file.cs",
            "a/**/b.txt",
            "file[0-9].txt",
            "file?.txt",
            @"literal\*.txt",
        ];

        return patterns[random.Next(patterns.Length)];
    }

    private static string GenerateArbitraryGlob(Random random)
    {
        const string chars = "abc[]!?*/\\.- ";
        var length = random.Next(0, 16);
        var builder = new StringBuilder(length);
        for (var i = 0; i < length; i++)
        {
            builder.Append(chars[random.Next(chars.Length)]);
        }

        return builder.ToString();
    }
}
