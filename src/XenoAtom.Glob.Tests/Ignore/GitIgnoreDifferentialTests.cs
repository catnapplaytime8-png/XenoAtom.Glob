// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Ignore;
using XenoAtom.Glob.Tests.TestInfrastructure;

namespace XenoAtom.Glob.Tests.Ignore;

[TestClass]
public class GitIgnoreDifferentialTests
{
    [TestMethod]
    public void GitDifferential_ShouldMatchNestedGitIgnoreBehavior()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        tempDirectory.WriteAllText(".gitignore", """
            *.log
            temp/
            !keep.log
            """);
        tempDirectory.WriteAllText("src/.gitignore", """
            generated/
            !generated/include.txt
            special.txt
            """);
        tempDirectory.WriteAllText(".git/info/exclude", """
            ignored-by-info.txt
            """);

        var rootRuleSet = IgnoreRuleSet.ParseGitIgnore(
            File.ReadAllText(tempDirectory.GetPath(".gitignore")),
            sourcePath: ".gitignore",
            sourceKind: IgnoreRuleSourceKind.PerDirectory);
        var srcRuleSet = IgnoreRuleSet.ParseGitIgnore(
            File.ReadAllText(tempDirectory.GetPath("src/.gitignore")),
            baseDirectory: "src",
            sourcePath: "src/.gitignore",
            sourceKind: IgnoreRuleSourceKind.PerDirectory);
        var infoRuleSet = IgnoreRuleSet.ParseGitIgnore(
            File.ReadAllText(tempDirectory.GetPath(".git/info/exclude")),
            sourcePath: ".git/info/exclude",
            sourceKind: IgnoreRuleSourceKind.RepositoryExclude);
        var matcher = new IgnoreMatcher(rootRuleSet, infoRuleSet, srcRuleSet);

        var paths = new[]
        {
            "trace.log",
            "keep.log",
            "temp/data.txt",
            "src/generated/code.cs",
            "src/generated/include.txt",
            "src/special.txt",
            "ignored-by-info.txt",
            "src/normal.txt",
        };

        var gitResults = QueryGit(git, paths);
        foreach (var path in paths)
        {
            var localResult = matcher.Evaluate(path);
            var gitResult = gitResults[path];

            Assert.AreEqual(
                gitResult.IsIgnored,
                localResult.IsIgnored,
                $"Mismatch for path '{path}' with Git version '{GitCli.Version}'.");

            if (gitResult.IsIgnored)
            {
                Assert.IsNotNull(localResult.Rule, $"Expected a winning rule for '{path}'.");
                Assert.AreEqual(gitResult.Pattern, localResult.Rule!.RawPatternText);
            }
        }
    }

    [TestMethod]
    public void GitDifferential_ShouldMatchGlobalExcludeBehavior()
    {
        using var tempDirectory = new TemporaryDirectory();
        using var configDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        var globalIgnorePath = configDirectory.GetPath("global-ignore.txt");
        File.WriteAllText(globalIgnorePath, "*.cache\n");

        var result = git.RunChecked("-c", $"core.excludesFile={globalIgnorePath}", "check-ignore", "--no-index", "-v", "folder/test.cache");
        Assert.AreEqual(0, result.ExitCode);

        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore(
            File.ReadAllText(globalIgnorePath),
            sourcePath: globalIgnorePath,
            sourceKind: IgnoreRuleSourceKind.GlobalExclude));

        var localResult = matcher.Evaluate("folder/test.cache");
        Assert.IsTrue(localResult.IsIgnored);
        Assert.AreEqual("*.cache", localResult.Rule!.PatternText);
    }

    [TestMethod]
    public void GitDifferential_ShouldMatchCrLfAndEscapedSpaceBehavior()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        File.WriteAllText(
            tempDirectory.GetPath(".gitignore"),
            "space\\ \r\nname.txt\r\n!keep.txt\r\n",
            new System.Text.UTF8Encoding(false));

        var ruleSet = IgnoreRuleSet.ParseGitIgnore(
            File.ReadAllText(tempDirectory.GetPath(".gitignore")),
            sourcePath: ".gitignore");
        var matcher = new IgnoreMatcher(ruleSet);
        var paths = new[] { "space ", "name.txt", "keep.txt" };
        var gitResults = QueryGit(git, paths);

        foreach (var path in paths)
        {
            var localResult = matcher.Evaluate(path);
            var gitResult = gitResults[path];
            Assert.AreEqual(gitResult.IsIgnored, localResult.IsIgnored, $"Mismatch for path '{path}' with Git version '{GitCli.Version}'.");
        }
    }

    [TestMethod]
    public void GitDifferential_ShouldMatchEscapedBangAndRecursiveWildcardBehavior()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        tempDirectory.WriteAllText(".gitignore", """
            \!literal.txt
            a/**/b.txt
            """);

        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore(
            File.ReadAllText(tempDirectory.GetPath(".gitignore")),
            sourcePath: ".gitignore"));
        var paths = new[] { "!literal.txt", "a/b.txt", "a/x/y/b.txt", "a/x/y/c.txt" };
        var gitResults = QueryGit(git, paths);

        foreach (var path in paths)
        {
            var localResult = matcher.Evaluate(path);
            var gitResult = gitResults[path];
            Assert.AreEqual(gitResult.IsIgnored, localResult.IsIgnored, $"Mismatch for path '{path}' with Git version '{GitCli.Version}'.");
        }
    }

    private static Dictionary<string, GitCheckIgnoreResult> QueryGit(GitCli git, IReadOnlyList<string> paths)
    {
        var input = string.Join('\0', paths) + '\0';
        var result = git.RunCheckedWithInput(input, "check-ignore", "--no-index", "--stdin", "-z", "-v", "--non-matching");
        var tokens = result.StandardOutput.Split('\0');
        var map = new Dictionary<string, GitCheckIgnoreResult>(StringComparer.Ordinal);

        for (var index = 0; index + 3 < tokens.Length; index += 4)
        {
            var source = tokens[index];
            var lineNumber = tokens[index + 1];
            var pattern = tokens[index + 2];
            var path = tokens[index + 3];
            if (path.Length == 0)
            {
                continue;
            }

            map[path] = string.IsNullOrEmpty(pattern)
                ? new GitCheckIgnoreResult(false, null, null, null)
                : new GitCheckIgnoreResult(!pattern.StartsWith('!'), source, lineNumber, pattern);
        }

        return map;
    }

    private readonly record struct GitCheckIgnoreResult(bool IsIgnored, string? Source, string? LineNumber, string? Pattern);
}
