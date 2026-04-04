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
    public void ParseGitIgnore_ShouldPreserveEscapedTrailingSpaces()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("space\\ \n"));

        var result = matcher.Evaluate("space ");

        Assert.IsTrue(result.IsIgnored);
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
    public void Evaluate_ShouldAnchorLeadingSlashPatternsToRuleBaseDirectory()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("/hello.*\n"));

        var rootMatch = matcher.Evaluate("hello.txt");
        var nestedMatch = matcher.Evaluate("a/hello.java");

        Assert.IsTrue(rootMatch.IsIgnored);
        Assert.IsFalse(nestedMatch.IsMatch);
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
}
