// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Ignore;
using XenoAtom.Glob.Tests.TestInfrastructure;

namespace XenoAtom.Glob.Tests.Ignore;

[TestClass]
public class IgnoreMatcherTests
{
    [TestMethod]
    public void ParseGitIgnore_ShouldIgnoreCommentsAndBlankLines()
    {
        var ruleSet = IgnoreRuleSet.ParseGitIgnore("""
            # comment

            *.obj
            """);

        Assert.AreEqual(1, ruleSet.Rules.Count);
        Assert.AreEqual("*.obj", ruleSet.Rules[0].PatternText);
    }

    [TestMethod]
    public void ParseGitIgnore_ShouldReturnEmptyRuleSetForEmptyOrWhitespaceOnlyContent()
    {
        var empty = IgnoreRuleSet.ParseGitIgnore(string.Empty);
        var whitespaceOnly = IgnoreRuleSet.ParseGitIgnore("   \n# comment\n \n");

        Assert.AreEqual(0, empty.Rules.Count);
        Assert.AreEqual(0, whitespaceOnly.Rules.Count);
    }

    [TestMethod]
    public void ParseGitIgnore_ShouldPopulateRuleMetadata()
    {
        var ruleSet = IgnoreRuleSet.ParseGitIgnore(
            " \nbin/\nsrc/generated/*.g.cs\nvendor\n",
            baseDirectory: @"src\app",
            sourcePath: ".gitignore",
            sourceKind: IgnoreRuleSourceKind.CommandLine);

        Assert.AreEqual(3, ruleSet.Rules.Count);

        var directoryOnlyRule = ruleSet.Rules[0];
        Assert.AreEqual("bin", directoryOnlyRule.PatternText);
        Assert.AreEqual("bin/", directoryOnlyRule.RawPatternText);
        Assert.IsTrue(directoryOnlyRule.DirectoryOnly);
        Assert.IsTrue(directoryOnlyRule.BasenameOnly);
        Assert.AreEqual("src/app", directoryOnlyRule.BaseDirectory);
        Assert.AreEqual(2, directoryOnlyRule.LineNumber);
        Assert.AreEqual(".gitignore", directoryOnlyRule.SourcePath);
        Assert.AreEqual(IgnoreRuleSourceKind.CommandLine, directoryOnlyRule.SourceKind);

        var pathRule = ruleSet.Rules[1];
        Assert.AreEqual("src/generated/*.g.cs", pathRule.PatternText);
        Assert.IsFalse(pathRule.DirectoryOnly);
        Assert.IsFalse(pathRule.BasenameOnly);
        Assert.AreEqual(3, pathRule.LineNumber);
        Assert.AreEqual(IgnoreRuleSourceKind.CommandLine, pathRule.SourceKind);

        var basenameRule = ruleSet.Rules[2];
        Assert.AreEqual("vendor", basenameRule.PatternText);
        Assert.IsFalse(basenameRule.DirectoryOnly);
        Assert.IsTrue(basenameRule.BasenameOnly);
        Assert.AreEqual(4, basenameRule.LineNumber);
        Assert.AreEqual(IgnoreRuleSourceKind.CommandLine, basenameRule.SourceKind);
    }

    [TestMethod]
    public void Evaluate_ShouldMatchSimpleExclude()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("*.obj"));

        var result = matcher.Evaluate("build/file.obj");

        Assert.IsTrue(result.IsMatch);
        Assert.IsTrue(result.IsIgnored);
        Assert.AreEqual("*.obj", result.Rule!.PatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldRespectNegationWithinOneFile()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
            *.obj
            !keep.obj
            """));

        var ignored = matcher.Evaluate("build/file.obj");
        var included = matcher.Evaluate("build/keep.obj");

        Assert.IsTrue(ignored.IsIgnored);
        Assert.IsTrue(included.IsMatch);
        Assert.IsFalse(included.IsIgnored);
        Assert.AreEqual("!keep.obj", included.Rule!.RawPatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldRespectDirectoryOnlyRules()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("bin/"));

        var directoryResult = matcher.Evaluate("src/bin", isDirectory: true);
        var childResult = matcher.Evaluate("src/bin/file.txt");
        var fileResult = matcher.Evaluate("src/bin", isDirectory: false);

        Assert.IsTrue(directoryResult.IsIgnored);
        Assert.IsTrue(childResult.IsIgnored);
        Assert.IsFalse(fileResult.IsIgnored);
    }

    [TestMethod]
    public void Evaluate_ShouldRespectBaseDirectory()
    {
        var rootRules = IgnoreRuleSet.ParseGitIgnore("*.tmp");
        var nestedRules = IgnoreRuleSet.ParseGitIgnore("special.txt", baseDirectory: "src");
        var matcher = new IgnoreMatcher(rootRules, nestedRules);

        var nestedResult = matcher.Evaluate("src/special.txt");
        var otherResult = matcher.Evaluate("other/special.txt");

        Assert.IsTrue(nestedResult.IsIgnored);
        Assert.IsFalse(otherResult.IsMatch);
    }

    [TestMethod]
    public void Evaluate_ShouldUseAncestorEvaluationForBasenameRules()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("vendor"));

        var result = matcher.Evaluate("src/vendor/file.txt");

        Assert.IsTrue(result.IsIgnored);
        Assert.AreEqual("vendor", result.Rule!.PatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldNotTreatBasenameRulesAsPrefixMatches()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("vendor"));

        var exactResult = matcher.Evaluate("src/vendor/file.txt");
        var prefixedResult = matcher.Evaluate("src/vendors/file.txt");
        var suffixedResult = matcher.Evaluate("src/myvendor/file.txt");

        Assert.IsTrue(exactResult.IsIgnored);
        Assert.IsFalse(prefixedResult.IsMatch);
        Assert.IsFalse(suffixedResult.IsMatch);
    }

    [TestMethod]
    public void Evaluate_ShouldPreservePrecedenceForIndexedBasenameRules()
    {
        var rules = Enumerable.Range(0, 40)
            .Select(static index => $"*.noise{index}")
            .Append("*.tmp")
            .Append("!keep.tmp");
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore(string.Join('\n', rules)));

        var ignored = matcher.Evaluate("build/file.tmp");
        var included = matcher.Evaluate("build/keep.tmp");

        Assert.IsTrue(ignored.IsIgnored);
        Assert.AreEqual("*.tmp", ignored.Rule!.PatternText);
        Assert.IsTrue(included.IsMatch);
        Assert.IsFalse(included.IsIgnored);
        Assert.AreEqual("!keep.tmp", included.Rule!.RawPatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldAllowFallbackRulesToOverrideIndexedBasenameRules()
    {
        var rules = Enumerable.Range(0, 40)
            .Select(static index => $"*.noise{index}")
            .Append("*.tmp")
            .Append("!src/**/keep.tmp");
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore(string.Join('\n', rules)));

        var ignored = matcher.Evaluate("other/keep.tmp");
        var included = matcher.Evaluate("src/deep/keep.tmp");

        Assert.IsTrue(ignored.IsIgnored);
        Assert.AreEqual("*.tmp", ignored.Rule!.PatternText);
        Assert.IsTrue(included.IsMatch);
        Assert.IsFalse(included.IsIgnored);
        Assert.AreEqual("!src/**/keep.tmp", included.Rule!.RawPatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldMatchIndexedBasenameRulesWithMultiDotSuffixes()
    {
        var rules = Enumerable.Range(0, 40)
            .Select(static index => $"*.noise{index}")
            .Append("*.nuget.g.props")
            .Append("*.nuget.g.targets");
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore(string.Join('\n', rules)));

        var propsResult = matcher.Evaluate("src/project.csproj.nuget.g.props");
        var targetsResult = matcher.Evaluate("src/project.csproj.nuget.g.targets");

        Assert.IsTrue(propsResult.IsIgnored);
        Assert.AreEqual("*.nuget.g.props", propsResult.Rule!.PatternText);
        Assert.IsTrue(targetsResult.IsIgnored);
        Assert.AreEqual("*.nuget.g.targets", targetsResult.Rule!.PatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldStopAtIgnoredAncestorDirectory()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
            cache/
            !cache/include.txt
            """));

        var result = matcher.Evaluate("cache/include.txt");

        Assert.IsTrue(result.IsIgnored);
        Assert.AreEqual("cache/", result.Rule!.RawPatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldAllowNegatedDirectoryUnderSingleStarPatternToKeepChildrenReachable()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
            foo/*
            !foo/bar
            """));

        var fooDirectory = matcher.Evaluate("foo", isDirectory: true);
        var barDirectory = matcher.Evaluate("foo/bar", isDirectory: true);
        var barChild = matcher.Evaluate("foo/bar/baz.txt");
        var sibling = matcher.Evaluate("foo/other.txt");

        Assert.IsFalse(fooDirectory.IsIgnored);
        Assert.IsTrue(barDirectory.IsMatch);
        Assert.IsFalse(barDirectory.IsIgnored);
        Assert.AreEqual("!foo/bar", barDirectory.Rule!.RawPatternText);
        Assert.IsFalse(barChild.IsIgnored);
        Assert.IsTrue(sibling.IsIgnored);
    }

    [TestMethod]
    public void Evaluate_ShouldNotAllowNegatedDeepFileToEscapeIgnoredIntermediateDirectory()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
            foo/*
            !foo/bar/baz.txt
            """));

        var barDirectory = matcher.Evaluate("foo/bar", isDirectory: true);
        var bazFile = matcher.Evaluate("foo/bar/baz.txt");

        Assert.IsTrue(barDirectory.IsIgnored);
        Assert.AreEqual("foo/*", barDirectory.Rule!.RawPatternText);
        Assert.IsTrue(bazFile.IsIgnored);
        Assert.AreEqual("foo/*", bazFile.Rule!.RawPatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldNotTreatTrailingDoubleStarAsIgnoringTheDirectoryItself()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
            tools/**
            !tools/packages.config
            """));

        var directoryResult = matcher.Evaluate("tools", isDirectory: true);
        var restoredFileResult = matcher.Evaluate("tools/packages.config");
        var otherFileResult = matcher.Evaluate("tools/other.txt");

        Assert.IsFalse(directoryResult.IsIgnored);
        Assert.IsTrue(restoredFileResult.IsMatch);
        Assert.IsFalse(restoredFileResult.IsIgnored);
        Assert.AreEqual("!tools/packages.config", restoredFileResult.Rule!.RawPatternText);
        Assert.IsTrue(otherFileResult.IsIgnored);
        Assert.AreEqual("tools/**", otherFileResult.Rule!.RawPatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldTreatSingleStarChildPatternAsIgnoringImmediateChildrenOnly()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("foo/*"));

        var fooDirectory = matcher.Evaluate("foo", isDirectory: true);
        var childFile = matcher.Evaluate("foo/file.txt");
        var grandChildFile = matcher.Evaluate("foo/bar/file.txt");

        Assert.IsFalse(fooDirectory.IsIgnored);
        Assert.IsTrue(childFile.IsIgnored);
        Assert.IsTrue(grandChildFile.IsIgnored);
        Assert.AreEqual("foo/*", grandChildFile.Rule!.RawPatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldAllowNegatedDirectoryExceptionToKeepChildrenReachable()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
            [Dd]ebug/
            !src/coreclr/debug
            """));

        var directoryResult = matcher.Evaluate("src/coreclr/debug", isDirectory: true);
        var childResult = matcher.Evaluate("src/coreclr/debug/CMakeLists.txt");

        Assert.IsTrue(directoryResult.IsMatch);
        Assert.IsFalse(directoryResult.IsIgnored);
        Assert.AreEqual("!src/coreclr/debug", directoryResult.Rule!.RawPatternText);
        Assert.IsFalse(childResult.IsIgnored);
    }

    [TestMethod]
    public void Evaluate_ShouldPreferLaterRuleAfterNegation()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
            *.tmp
            !keep.tmp
            keep.tmp
            """));

        var result = matcher.Evaluate("keep.tmp");

        Assert.IsTrue(result.IsIgnored);
        Assert.AreEqual("keep.tmp", result.Rule!.PatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldAllowNegatedFileUnderDoubleStarDirectoryPattern()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
            **/.vscode/**
            !**/.vscode/c_cpp_properties.json
            """));

        var directoryResult = matcher.Evaluate("src/vm/.vscode", isDirectory: true);
        var restoredFileResult = matcher.Evaluate("src/vm/.vscode/c_cpp_properties.json");
        var ignoredFileResult = matcher.Evaluate("src/vm/.vscode/settings.json");

        Assert.IsFalse(directoryResult.IsIgnored);
        Assert.IsTrue(restoredFileResult.IsMatch);
        Assert.IsFalse(restoredFileResult.IsIgnored);
        Assert.AreEqual("!**/.vscode/c_cpp_properties.json", restoredFileResult.Rule!.RawPatternText);
        Assert.IsTrue(ignoredFileResult.IsIgnored);
    }

    [TestMethod]
    public void Evaluate_ShouldMatchRecursiveWildcardWithZeroOrMoreSegments()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("a/**/b.txt"));

        var directChild = matcher.Evaluate("a/b.txt");
        var nestedChild = matcher.Evaluate("a/x/y/b.txt");
        var nonMatch = matcher.Evaluate("a/x/y/c.txt");

        Assert.IsTrue(directChild.IsIgnored);
        Assert.IsTrue(nestedChild.IsIgnored);
        Assert.IsFalse(nonMatch.IsMatch);
    }

    [TestMethod]
    public void Evaluate_ShouldRespectRuleSetPrecedence()
    {
        var lowPrecedence = IgnoreRuleSet.ParseGitIgnore("*.log");
        var highPrecedence = IgnoreRuleSet.ParseGitIgnore("!app.log");
        var matcher = new IgnoreMatcher(lowPrecedence, highPrecedence);

        var result = matcher.Evaluate("app.log");

        Assert.IsTrue(result.IsMatch);
        Assert.IsFalse(result.IsIgnored);
        Assert.AreEqual("!app.log", result.Rule!.RawPatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldAllowHigherPrecedenceNestedRuleSetToOverrideRootRules()
    {
        var rootRules = IgnoreRuleSet.ParseGitIgnore("*.tmp");
        var nestedRules = IgnoreRuleSet.ParseGitIgnore("!keep.tmp", baseDirectory: "src");
        var matcher = new IgnoreMatcher(rootRules, nestedRules);

        var nestedResult = matcher.Evaluate("src/keep.tmp");
        var otherResult = matcher.Evaluate("other/keep.tmp");

        Assert.IsTrue(nestedResult.IsMatch);
        Assert.IsFalse(nestedResult.IsIgnored);
        Assert.AreEqual("!keep.tmp", nestedResult.Rule!.RawPatternText);
        Assert.IsTrue(otherResult.IsIgnored);
    }

    [TestMethod]
    public void ParseGitIgnore_ShouldPreserveEscapedTrailingSpaces()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("space\\ \n"));

        var result = matcher.Evaluate("space ");

        Assert.IsTrue(result.IsIgnored);
    }

    [TestMethod]
    public void ParseGitIgnore_ShouldTrimUnescapedTrailingSpaces()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("trimmed.txt   \n"));

        var trimmed = matcher.Evaluate("trimmed.txt");
        var literalSpaces = matcher.Evaluate("trimmed.txt   ");

        Assert.IsTrue(trimmed.IsIgnored);
        Assert.IsFalse(literalSpaces.IsMatch);
    }

    [TestMethod]
    public void ParseGitIgnore_ShouldTreatEscapedHashAsLiteral()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("\\#literal.txt\n"));

        var ignored = matcher.Evaluate("#literal.txt");
        var other = matcher.Evaluate("literal.txt");

        Assert.IsTrue(ignored.IsIgnored);
        Assert.IsFalse(other.IsMatch);
        Assert.AreEqual("\\#literal.txt", ignored.Rule!.PatternText);
        Assert.AreEqual("\\#literal.txt", ignored.Rule.RawPatternText);
    }

    [TestMethod]
    public void ParseGitIgnore_ShouldTreatEscapedBangAsLiteral()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("\\!literal.txt\n"));

        var ignored = matcher.Evaluate("!literal.txt");
        var negatedLooking = matcher.Evaluate("literal.txt");

        Assert.IsTrue(ignored.IsIgnored);
        Assert.IsFalse(negatedLooking.IsMatch);
        Assert.AreEqual("\\!literal.txt", ignored.Rule!.PatternText);
        Assert.AreEqual("\\!literal.txt", ignored.Rule.RawPatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldAnchorLeadingSlashPatternsToRuleBaseDirectory()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("/hello.*\n"));

        var rootMatch = matcher.Evaluate("hello.txt");
        var nestedMatch = matcher.Evaluate("a/hello.java");

        Assert.IsTrue(rootMatch.IsIgnored);
        Assert.IsFalse(nestedMatch.IsMatch);
    }

    [TestMethod]
    public void Evaluate_ShouldAnchorLeadingSlashPatternsToNestedBaseDirectory()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("/generated/\n", baseDirectory: "src"));

        var anchoredMatch = matcher.Evaluate("src/generated/file.cs");
        var nestedNonMatch = matcher.Evaluate("src/deep/generated/file.cs");
        var outsideBaseNonMatch = matcher.Evaluate("other/generated/file.cs");

        Assert.IsTrue(anchoredMatch.IsIgnored);
        Assert.IsFalse(nestedNonMatch.IsMatch);
        Assert.IsFalse(outsideBaseNonMatch.IsMatch);
    }

    [TestMethod]
    public void Evaluate_ShouldTreatLeadingSlashAsRedundantWhenPatternHasMiddleSlash()
    {
        var matcherWithoutLeadingSlash = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("doc/frotz\n"));
        var matcherWithLeadingSlash = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("/doc/frotz\n"));
        var paths = new[] { "doc/frotz", "a/doc/frotz" };

        foreach (var path in paths)
        {
            var withoutLeadingSlash = matcherWithoutLeadingSlash.Evaluate(path);
            var withLeadingSlash = matcherWithLeadingSlash.Evaluate(path);

            Assert.AreEqual(withoutLeadingSlash.IsMatch, withLeadingSlash.IsMatch, $"Mismatch for path '{path}'.");
            Assert.AreEqual(withoutLeadingSlash.IsIgnored, withLeadingSlash.IsIgnored, $"Mismatch for path '{path}'.");
        }
    }

    [TestMethod]
    public void ParseGitIgnore_ShouldSupportStreamAndReaderOverloads()
    {
        using var stream = new MemoryStream("*.tmp\n"u8.ToArray());
        using var reader = new StringReader("!keep.tmp\n");
        var low = IgnoreRuleSet.ParseGitIgnore(stream);
        var high = IgnoreRuleSet.ParseGitIgnore(reader);
        var matcher = new IgnoreMatcher(low, high);

        var ignored = matcher.Evaluate("file.tmp");
        var included = matcher.Evaluate("keep.tmp");

        Assert.IsTrue(ignored.IsIgnored);
        Assert.IsFalse(included.IsIgnored);
    }

    [TestMethod]
    public void Parse_ShouldSupportReadOnlySpanOverload()
    {
        ReadOnlySpan<char> content = """
            *.tmp
            !keep.tmp
            """;

        var ruleSet = IgnoreRuleSet.Parse(
            content,
            IgnoreDialect.GitIgnore,
            sourcePath: ".gitignore",
            sourceKind: IgnoreRuleSourceKind.PerDirectory);
        var matcher = new IgnoreMatcher(ruleSet);

        var ignored = matcher.Evaluate("file.tmp");
        var included = matcher.Evaluate("keep.tmp");

        Assert.AreEqual(2, ruleSet.Rules.Count);
        Assert.IsTrue(ignored.IsIgnored);
        Assert.IsTrue(included.IsMatch);
        Assert.IsFalse(included.IsIgnored);
        Assert.AreEqual("!keep.tmp", included.Rule!.RawPatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldSupportReadOnlySpanOverload()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
            *.tmp
            !keep.tmp
            """));
        ReadOnlySpan<char> ignoredPath = @"src\obj\file.tmp";
        ReadOnlySpan<char> includedPath = @"src\obj\keep.tmp";

        var ignored = matcher.Evaluate(ignoredPath);
        var included = matcher.Evaluate(includedPath);

        Assert.IsTrue(ignored.IsIgnored);
        Assert.IsTrue(included.IsMatch);
        Assert.IsFalse(included.IsIgnored);
        Assert.AreEqual("!keep.tmp", included.Rule!.RawPatternText);
    }

    [TestMethod]
    public void Evaluate_ShouldSupportUnicodePaths()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("**/\u00E9t\u00E9.tmp\n"));

        var result = matcher.Evaluate("donn\u00E9es/\u00E9t\u00E9.tmp");
        var nonMatch = matcher.Evaluate("donnees/ete.tmp");

        Assert.IsTrue(result.IsIgnored);
        Assert.IsFalse(nonMatch.IsMatch);
    }

    [TestMethod]
    public void EvaluateWithReusableEvaluator_ShouldSupportLongReadOnlySpanPaths()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("**/target.tmp"));
        using var evaluator = matcher.CreateEvaluator();
        ReadOnlySpan<char> candidate = CreateLongRelativePath(segmentCount: 36, segmentLength: 8, leafName: "target.tmp");

        var result = evaluator.Evaluate(candidate);

        Assert.IsTrue(result.IsIgnored);
        Assert.AreEqual("**/target.tmp", result.Rule!.PatternText);
    }

    [TestMethod]
    public void EvaluateWithReusableEvaluator_ShouldMatchDefaultEvaluation()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
            *.tmp
            !keep.tmp
            src/**/generated/
            """));
        using var evaluator = matcher.CreateEvaluator();
        var paths = new[]
        {
            "file.tmp",
            "keep.tmp",
            "src/generated",
            "src/deep/generated/file.cs",
            "src/app/main.cs",
        };

        foreach (var path in paths)
        {
            Assert.AreEqual(matcher.Evaluate(path), evaluator.Evaluate(path), $"Mismatch for path '{path}'.");
        }
    }

    [TestMethod]
    public void EvaluateWithReusableEvaluator_ShouldAvoidManagedAllocationsForNormalizedPaths()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
            *.tmp
            !keep.tmp
            vendor/
            """));
        using var evaluator = matcher.CreateEvaluator();

        _ = evaluator.Evaluate("src/file.tmp");
        _ = evaluator.Evaluate("vendor/lib/file.cs");

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 512; index++)
        {
            _ = evaluator.Evaluate("src/file.tmp");
            _ = evaluator.Evaluate("vendor/lib/file.cs");
        }

        var after = GC.GetAllocatedBytesForCurrentThread();
        Assert.AreEqual(before, after);
    }

    [TestMethod]
    public void ParseGitIgnore_ShouldRejectInvalidTrailingEscape()
    {
        Assert.Throws<ArgumentException>(() => IgnoreRuleSet.ParseGitIgnore("broken\\"));
    }

    private static string CreateLongRelativePath(int segmentCount, int segmentLength, string leafName)
    {
        var segments = Enumerable.Repeat(new string('a', segmentLength), segmentCount);
        return string.Join('/', segments.Append(leafName));
    }
}
