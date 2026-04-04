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
    public void ParseGitIgnore_ShouldRejectInvalidTrailingEscape()
    {
        Assert.Throws<ArgumentException>(() => IgnoreRuleSet.ParseGitIgnore("broken\\"));
    }
}
