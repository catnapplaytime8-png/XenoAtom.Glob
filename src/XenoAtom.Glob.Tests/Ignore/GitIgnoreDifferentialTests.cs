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
            var gitResult = gitResults[path];
            GitCompatibilityAssert.Matches(path, false, git, gitResult, matcher, "nested-gitignore");
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
            var gitResult = gitResults[path];
            GitCompatibilityAssert.Matches(path, false, git, gitResult, matcher, "crlf-and-escaped-space");
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
            var gitResult = gitResults[path];
            GitCompatibilityAssert.Matches(path, false, git, gitResult, matcher, "escaped-bang-and-recursive");
        }
    }

    [TestMethod]
    public void GitDifferential_ShouldMatchGitignoreDocumentationPatternExamples()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        tempDirectory.WriteAllText(".gitignore", """
            \#literal.txt
            /hello.*
            doc/frotz
            foo/*
            **/foo
            abc/**
            a/**/b
            """);

        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore(
            File.ReadAllText(tempDirectory.GetPath(".gitignore")),
            sourcePath: ".gitignore"));
        var paths = new[]
        {
            "#literal.txt",
            "hello.txt",
            "a/hello.java",
            "doc/frotz",
            "a/doc/frotz",
            "foo/test.json",
            "foo/bar",
            "foo/bar/hello.c",
            "foo",
            "nested/foo",
            "abc/file.txt",
            "abc/nested/file.txt",
            "other/abc/file.txt",
            "a/b",
            "a/x/b",
            "a/x/y/b",
            "a/x/y/c",
        };
        var gitResults = QueryGit(git, paths);

        foreach (var path in paths)
        {
            GitCompatibilityAssert.Matches(path, false, git, gitResults[path], matcher, "gitignore-documentation-pattern-examples");
        }
    }

    [TestMethod]
    public void GitDifferential_ShouldNotFollowSymlinkedGitIgnoreFiles_WhenSupported()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        tempDirectory.WriteAllText("rules.txt", "*.tmp\n");
        tempDirectory.WriteAllText("file.tmp", string.Empty);

        try
        {
            File.CreateSymbolicLink(tempDirectory.GetPath(".gitignore"), "rules.txt");
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            Assert.Inconclusive($"Symbolic links are not supported in this environment: {ex.Message}");
        }

        var context = XenoAtom.Glob.Git.RepositoryDiscovery.Discover(tempDirectory.Path);
        var matcher = context.GetRepositoryIgnoreStack(string.Empty).Matcher;
        var gitQuery = git.RunWithInput("file.tmp\0", "check-ignore", "--no-index", "--stdin", "-z", "-v", "--non-matching");
        if (gitQuery.ExitCode != 0)
        {
            Assert.Inconclusive($"Git could not evaluate a symlinked .gitignore on this platform: {gitQuery.StandardError.Trim()}");
        }

        var gitResult = QueryGit(git, ["file.tmp"])["file.tmp"];

        GitCompatibilityAssert.Matches("file.tmp", false, git, gitResult, matcher, "symlinked-gitignore-file");
    }

    [TestMethod]
    public void GitDifferential_ShouldMatchDocumentationExcludeEverythingExceptDirectoryExample()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        tempDirectory.WriteAllText(".gitignore", """
            /*
            !/foo
            /foo/*
            !/foo/bar
            """);
        tempDirectory.CreateDirectory("foo", "bar");
        tempDirectory.WriteAllText("foo/file.txt", string.Empty);
        tempDirectory.WriteAllText("foo/bar/keep.txt", string.Empty);
        tempDirectory.WriteAllText("root.txt", string.Empty);

        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore(
            File.ReadAllText(tempDirectory.GetPath(".gitignore")),
            sourcePath: ".gitignore"));
        var entries = new (string Path, bool IsDirectory)[]
        {
            ("root.txt", false),
            ("foo", true),
            ("foo/file.txt", false),
            ("foo/bar", true),
            ("foo/bar/keep.txt", false),
        };
        var gitResults = QueryGit(git, entries.Select(static entry => entry.Path).ToArray());

        foreach (var entry in entries)
        {
            GitCompatibilityAssert.Matches(
                entry.Path,
                entry.IsDirectory,
                git,
                gitResults[entry.Path],
                matcher,
                "gitignore-documentation-exclude-everything-except-directory");
        }
    }

    [TestMethod]
    public void GitDifferential_ShouldMatchCorpusFixtures()
    {
        foreach (var fixture in GitIgnoreCorpus.Load())
        {
            using var tempDirectory = new TemporaryDirectory();
            var git = GitCli.In(tempDirectory.Path);
            git.RunChecked("init", "--quiet");

            foreach (var file in fixture.Files)
            {
                tempDirectory.WriteAllText(file.Key, file.Value);
            }

            var matcher = BuildMatcherFromRepository(tempDirectory);
            var gitResults = QueryGit(git, fixture.Paths);
            foreach (var path in fixture.Paths)
            {
                GitCompatibilityAssert.Matches(path, false, git, gitResults[path], matcher, fixture.Name);
            }
        }
    }

    [TestMethod]
    public void GitDifferential_ShouldMatchCaseSensitivityOnCurrentPlatform()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");
        tempDirectory.WriteAllText(".gitignore", "*.TXT\n");

        var matcher = BuildMatcherFromRepository(tempDirectory);

        var paths = new[] { "file.txt", "file.TXT" };
        var gitResults = QueryGit(git, paths);
        foreach (var path in paths)
        {
            GitCompatibilityAssert.Matches(path, false, git, gitResults[path], matcher, "case-sensitivity-current-platform-default");
        }
    }

    [TestMethod]
    public void GitDifferential_ShouldMatchConfiguredCoreIgnoreCaseBehavior()
    {
        AssertCaseSensitivityScenario(ignoreCase: false, "configured-case-sensitive");
        AssertCaseSensitivityScenario(ignoreCase: true, "configured-case-insensitive");
    }

    [TestMethod]
    public void GitDifferential_ShouldMatchCharacterClassesWhenCoreIgnoreCaseChanges()
    {
        AssertCharacterClassScenario(ignoreCase: false, "character-classes-case-sensitive");
        AssertCharacterClassScenario(ignoreCase: true, "character-classes-case-insensitive");
    }

    [TestMethod]
    public void GitDifferential_ShouldMatchGeneratedRuleOrderingAndNegationScenarios()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        var random = new Random(97);
        string[] rules =
        [
            "*.tmp",
            "*.log",
            "build/",
            "cache/",
            "!keep.tmp",
            "!important.log",
            "src/generated/",
            "!src/generated/include.txt",
            "docs/**/*.bak",
            "!docs/reference/keep.bak",
        ];
        string[] paths =
        [
            "file.tmp",
            "keep.tmp",
            "trace.log",
            "important.log",
            "build/output.bin",
            "cache/entry.dat",
            "src/generated/code.cs",
            "src/generated/include.txt",
            "docs/reference/a.bak",
            "docs/reference/keep.bak",
            "src/app/main.cs",
        ];

        for (var iteration = 0; iteration < 50; iteration++)
        {
            var selectedRules = SelectRules(random, rules);
            tempDirectory.WriteAllText(".gitignore", string.Join('\n', selectedRules) + "\n");

            var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore(
                File.ReadAllText(tempDirectory.GetPath(".gitignore")),
                sourcePath: ".gitignore"));

            var gitResults = QueryGit(git, paths);
            foreach (var path in paths)
            {
                GitCompatibilityAssert.Matches(path, false, git, gitResults[path], matcher, $"generated-ordering-{iteration}");
            }
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

    private static string[] SelectRules(Random random, string[] rules)
    {
        var count = random.Next(1, rules.Length + 1);
        var selected = rules
            .OrderBy(_ => random.Next())
            .Take(count)
            .ToArray();
        return selected;
    }

    private static IgnoreMatcher BuildMatcherFromRepository(TemporaryDirectory tempDirectory)
    {
        var context = XenoAtom.Glob.Git.RepositoryDiscovery.Discover(tempDirectory.Path);
        var ruleSets = new List<IgnoreRuleSet>();
        var globalExclude = tempDirectory.GetPath("global-ignore.txt");
        if (File.Exists(globalExclude))
        {
            ruleSets.Add(IgnoreRuleSet.ParseGitIgnore(
                File.ReadAllText(globalExclude),
                sourcePath: globalExclude,
                sourceKind: IgnoreRuleSourceKind.GlobalExclude));
        }

        var infoExclude = tempDirectory.GetPath(".git", "info", "exclude");
        if (File.Exists(infoExclude))
        {
            ruleSets.Add(IgnoreRuleSet.ParseGitIgnore(
                File.ReadAllText(infoExclude),
                sourcePath: infoExclude,
                sourceKind: IgnoreRuleSourceKind.RepositoryExclude));
        }

        foreach (var gitIgnorePath in Directory.GetFiles(tempDirectory.Path, ".gitignore", SearchOption.AllDirectories)
                     .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var baseDirectory = Path.GetDirectoryName(gitIgnorePath)!;
            var relativeBase = Path.GetRelativePath(tempDirectory.Path, baseDirectory).Replace('\\', '/');
            if (relativeBase == ".")
            {
                relativeBase = string.Empty;
            }

            ruleSets.Add(IgnoreRuleSet.ParseGitIgnore(
                File.ReadAllText(gitIgnorePath),
                baseDirectory: relativeBase,
                sourcePath: gitIgnorePath,
                sourceKind: IgnoreRuleSourceKind.PerDirectory));
        }

        return new IgnoreMatcher(ruleSets, context.PathComparison);
    }

    private static void AssertCaseSensitivityScenario(bool ignoreCase, string scenarioName)
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");
        git.RunChecked("config", "core.ignorecase", ignoreCase ? "true" : "false");
        tempDirectory.WriteAllText(".gitignore", "*.TXT\n");

        var matcher = BuildMatcherFromRepository(tempDirectory);
        var paths = new[] { "file.txt", "file.TXT" };
        var gitResults = QueryGit(git, paths);
        foreach (var path in paths)
        {
            GitCompatibilityAssert.Matches(path, false, git, gitResults[path], matcher, scenarioName);
        }
    }

    private static void AssertCharacterClassScenario(bool ignoreCase, string scenarioName)
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");
        git.RunChecked("config", "core.ignorecase", ignoreCase ? "true" : "false");
        tempDirectory.WriteAllText(".gitignore", """
            [A-Z].txt
            [a-z].cs
            [!A-Z].bin
            """);

        var matcher = BuildMatcherFromRepository(tempDirectory);
        var paths = new[] { "a.txt", "A.txt", "a.cs", "A.cs", "a.bin", "A.bin", "1.bin" };
        var gitResults = QueryGit(git, paths);
        foreach (var path in paths)
        {
            GitCompatibilityAssert.Matches(path, false, git, gitResults[path], matcher, scenarioName);
        }
    }
}
