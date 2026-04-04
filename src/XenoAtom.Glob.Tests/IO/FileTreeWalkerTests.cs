// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Git;
using XenoAtom.Glob.IO;
using XenoAtom.Glob.Tests.TestInfrastructure;

namespace XenoAtom.Glob.Tests.IO;

[TestClass]
public class FileTreeWalkerTests
{
    [TestMethod]
    public void Enumerate_ShouldReturnAllFilesWhenNoIgnoreRulesExist()
    {
        using var tempDirectory = new TemporaryDirectory();
        tempDirectory.WriteAllText("src/app.cs", string.Empty);
        tempDirectory.WriteAllText("src/lib/util.cs", string.Empty);

        var walker = new FileTreeWalker();
        var entries = walker.Enumerate(tempDirectory.Path)
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "src/app.cs", "src/lib/util.cs" }, entries);
    }

    [TestMethod]
    public void Enumerate_ShouldPruneIgnoredDirectories()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        tempDirectory.WriteAllText(".gitignore", """
            obj/
            *.tmp
            """);
        tempDirectory.WriteAllText("src/.gitignore", """
            generated/
            !generated/include.txt
            """);

        tempDirectory.WriteAllText("src/app.cs", string.Empty);
        tempDirectory.WriteAllText("src/generated/file.cs", string.Empty);
        tempDirectory.WriteAllText("src/generated/include.txt", string.Empty);
        tempDirectory.WriteAllText("obj/build.bin", string.Empty);
        tempDirectory.WriteAllText("root.tmp", string.Empty);

        var walker = new FileTreeWalker();
        var context = RepositoryDiscovery.Discover(tempDirectory.Path);
        var entries = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { RepositoryContext = context })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { ".gitignore", "src/.gitignore", "src/app.cs" }, entries);
    }

    [TestMethod]
    public void Enumerate_ShouldMatchGitVisibleFileSet()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        tempDirectory.WriteAllText(".gitignore", """
            *.log
            temp/
            """);
        tempDirectory.WriteAllText("src/.gitignore", """
            generated/
            special.txt
            """);
        tempDirectory.WriteAllText(".git/info/exclude", "ignored-by-info.txt\n");

        tempDirectory.WriteAllText("src/app.cs", string.Empty);
        tempDirectory.WriteAllText("src/special.txt", string.Empty);
        tempDirectory.WriteAllText("src/generated/code.cs", string.Empty);
        tempDirectory.WriteAllText("temp/cache.bin", string.Empty);
        tempDirectory.WriteAllText("trace.log", string.Empty);
        tempDirectory.WriteAllText("ignored-by-info.txt", string.Empty);
        tempDirectory.WriteAllText("README.md", string.Empty);

        var allFiles = Directory.GetFiles(tempDirectory.Path, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(tempDirectory.Path, path).Replace('\\', '/'))
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        var expected = QueryVisiblePathsFromGit(git, allFiles);

        var walker = new FileTreeWalker();
        var context = RepositoryDiscovery.Discover(tempDirectory.Path);
        var actual = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { RepositoryContext = context })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Enumerate_ShouldHandleReusableRealisticRepositoryFixture()
    {
        using var fixture = new RepositoryFixtureBuilder();
        fixture.CreateTypicalRepository()
            .CreateDeepTree(depth: 6, filesPerDirectory: 2)
            .CreateWideTree(directoryCount: 10, filesPerDirectory: 3);

        var git = GitCli.In(fixture.Root.Path);
        git.RunChecked("init", "--quiet");

        var walker = new FileTreeWalker();
        var context = RepositoryDiscovery.Discover(fixture.Root.Path);
        var entries = walker.Enumerate(fixture.Root.Path, new FileTreeWalkOptions { RepositoryContext = context }).ToArray();

        Assert.IsTrue(entries.Any(static x => x.RelativePath == "README.md"));
        Assert.IsTrue(entries.Any(static x => x.RelativePath == "src/app/app.cs"));
        Assert.IsTrue(entries.Any(static x => x.RelativePath == "src/generated/include.txt"));
        Assert.IsFalse(entries.Any(static x => x.RelativePath.StartsWith("bin/", StringComparison.Ordinal)));
        Assert.IsFalse(entries.Any(static x => x.RelativePath.StartsWith("obj/", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Enumerate_ShouldHandleManySmallGitIgnoreFiles()
    {
        using var fixture = new RepositoryFixtureBuilder();
        fixture.CreateTypicalRepository()
            .CreateManySmallIgnoreFiles(depth: 12, filesPerDirectory: 3);

        var git = GitCli.In(fixture.Root.Path);
        git.RunChecked("init", "--quiet");

        var allFiles = Directory.GetFiles(fixture.Root.Path, "*", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(fixture.Root.Path, path).Replace('\\', '/'))
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();
        var expected = QueryVisiblePathsFromGit(git, allFiles);

        var walker = new FileTreeWalker();
        var context = RepositoryDiscovery.Discover(fixture.Root.Path);
        var actual = walker.Enumerate(fixture.Root.Path, new FileTreeWalkOptions { RepositoryContext = context })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Enumerate_ShouldAllowReincludedChildrenWhenDirectoryIsReachable()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        tempDirectory.WriteAllText(".gitignore", """
            generated/*
            !generated/include.txt
            """);
        tempDirectory.WriteAllText("generated/include.txt", string.Empty);
        tempDirectory.WriteAllText("generated/other.txt", string.Empty);

        var walker = new FileTreeWalker();
        var context = RepositoryDiscovery.Discover(tempDirectory.Path);
        var entries = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { RepositoryContext = context })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { ".gitignore", "generated/include.txt" }, entries);
    }

    [TestMethod]
    public void Enumerate_ShouldHonorConfiguredCoreIgnoreCase()
    {
        AssertCaseSensitivityTraversal(ignoreCase: false);
        AssertCaseSensitivityTraversal(ignoreCase: true);
    }

    [TestMethod]
    public void Enumerate_ShouldExposeCapturedFileSystemMetadata()
    {
        using var tempDirectory = new TemporaryDirectory();
        tempDirectory.CreateDirectory("src");
        tempDirectory.WriteAllText("src/app.cs", "hello");

        var walker = new FileTreeWalker();
        var entries = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { IncludeDirectories = true }).ToArray();
        var directoryEntry = entries.Single(static x => x.RelativePath == "src");
        var fileEntry = entries.Single(static x => x.RelativePath == "src/app.cs");
        var filePath = tempDirectory.GetPath("src", "app.cs");
        var directoryPath = tempDirectory.GetPath("src");

        Assert.AreEqual("src", directoryEntry.Name);
        Assert.AreEqual(directoryPath, directoryEntry.FullPath);
        Assert.IsTrue(directoryEntry.IsDirectory);
        Assert.AreEqual(File.GetAttributes(directoryPath), directoryEntry.Attributes);
        Assert.AreEqual((directoryEntry.Attributes & FileAttributes.Hidden) != 0, directoryEntry.IsHidden);
        AssertTimestampClose(new DateTimeOffset(Directory.GetCreationTimeUtc(directoryPath), TimeSpan.Zero), directoryEntry.CreationTimeUtc);
        AssertTimestampClose(new DateTimeOffset(Directory.GetLastAccessTimeUtc(directoryPath), TimeSpan.Zero), directoryEntry.LastAccessTimeUtc);
        AssertTimestampClose(new DateTimeOffset(Directory.GetLastWriteTimeUtc(directoryPath), TimeSpan.Zero), directoryEntry.LastWriteTimeUtc);

        Assert.AreEqual("app.cs", fileEntry.Name);
        Assert.AreEqual(filePath, fileEntry.FullPath);
        Assert.IsFalse(fileEntry.IsDirectory);
        Assert.AreEqual(5L, fileEntry.Length);
        Assert.AreEqual(File.GetAttributes(filePath), fileEntry.Attributes);
        Assert.AreEqual((fileEntry.Attributes & FileAttributes.Hidden) != 0, fileEntry.IsHidden);
        AssertTimestampClose(new DateTimeOffset(File.GetCreationTimeUtc(filePath), TimeSpan.Zero), fileEntry.CreationTimeUtc);
        AssertTimestampClose(new DateTimeOffset(File.GetLastAccessTimeUtc(filePath), TimeSpan.Zero), fileEntry.LastAccessTimeUtc);
        AssertTimestampClose(new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero), fileEntry.LastWriteTimeUtc);
    }

    [TestMethod]
    public void Enumerate_ShouldInvalidateCachedIgnoreFilesWhenGitIgnoreChanges()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        tempDirectory.WriteAllText(".gitignore", "*.tmp\n");
        tempDirectory.WriteAllText("file.tmp", string.Empty);

        var walker = new FileTreeWalker();
        var context = RepositoryDiscovery.Discover(tempDirectory.Path);

        var firstPass = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { RepositoryContext = context })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { ".gitignore" }, firstPass);

        tempDirectory.WriteAllText(".gitignore", "*.log\n");
        var gitIgnorePath = tempDirectory.GetPath(".gitignore");
        File.SetLastWriteTimeUtc(gitIgnorePath, File.GetLastWriteTimeUtc(gitIgnorePath).AddSeconds(2));

        var secondPass = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { RepositoryContext = context })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { ".gitignore", "file.tmp" }, secondPass);
    }

    [TestMethod]
    public void Enumerate_ShouldInvalidateCachedIgnoreFilesWhenGitIgnoreAppears()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        tempDirectory.WriteAllText("file.tmp", string.Empty);

        var walker = new FileTreeWalker();
        var context = RepositoryDiscovery.Discover(tempDirectory.Path);

        var firstPass = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { RepositoryContext = context })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "file.tmp" }, firstPass);

        tempDirectory.WriteAllText(".gitignore", "*.tmp\n");

        var secondPass = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { RepositoryContext = context })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { ".gitignore" }, secondPass);
    }

    [TestMethod]
    public void Enumerate_ShouldInvalidateCachedIgnoreFilesWhenRepositoryExcludeChanges()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        tempDirectory.WriteAllText(".git/info/exclude", "file.tmp\n");
        tempDirectory.WriteAllText("file.tmp", string.Empty);

        var walker = new FileTreeWalker();
        var context = RepositoryDiscovery.Discover(tempDirectory.Path);

        var firstPass = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { RepositoryContext = context })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(Array.Empty<string>(), firstPass);

        tempDirectory.WriteAllText(".git/info/exclude", string.Empty);
        var excludePath = tempDirectory.GetPath(".git/info/exclude");
        File.SetLastWriteTimeUtc(excludePath, File.GetLastWriteTimeUtc(excludePath).AddSeconds(2));

        var secondPass = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { RepositoryContext = context })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "file.tmp" }, secondPass);
    }

    [TestMethod]
    public void Enumerate_ShouldHonorCancellation()
    {
        using var tempDirectory = new TemporaryDirectory();
        tempDirectory.WriteAllText("a.txt", string.Empty);

        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var walker = new FileTreeWalker();
        Assert.Throws<OperationCanceledException>(() =>
        {
            _ = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions
            {
                CancellationToken = cancellationTokenSource.Token,
            }).ToArray();
        });
    }

    [TestMethod]
    public void Enumerate_ShouldNotFollowDirectorySymlinkByDefault_WhenSupported()
    {
        using var tempDirectory = new TemporaryDirectory();
        tempDirectory.CreateDirectory("real");
        tempDirectory.WriteAllText("real/file.txt", string.Empty);

        try
        {
            Directory.CreateSymbolicLink(tempDirectory.GetPath("linked"), tempDirectory.GetPath("real"));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
        {
            Assert.Inconclusive($"Symbolic links are not supported in this environment: {ex.Message}");
        }

        var walker = new FileTreeWalker();
        var entries = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { IncludeDirectories = true })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(new[] { "real", "real/file.txt" }, entries);
    }

    private static string[] QueryVisiblePathsFromGit(GitCli git, IReadOnlyList<string> paths)
    {
        var input = string.Join('\0', paths) + '\0';
        var result = git.RunCheckedWithInput(input, "check-ignore", "--no-index", "--stdin", "-z", "-v", "--non-matching");
        var tokens = result.StandardOutput.Split('\0');
        var visiblePaths = new List<string>();
        for (var index = 0; index + 3 < tokens.Length; index += 4)
        {
            var pattern = tokens[index + 2];
            var path = tokens[index + 3];
            if (path.Length == 0)
            {
                continue;
            }

            if (pattern.Length == 0 || pattern[0] == '!')
            {
                visiblePaths.Add(path);
            }
        }

        visiblePaths.Sort(StringComparer.Ordinal);
        return visiblePaths.ToArray();
    }

    private static void AssertCaseSensitivityTraversal(bool ignoreCase)
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");
        git.RunChecked("config", "core.ignorecase", ignoreCase ? "true" : "false");

        tempDirectory.WriteAllText(".gitignore", "*.TXT\n");
        tempDirectory.WriteAllText("file.txt", string.Empty);
        tempDirectory.WriteAllText("file.TXT", string.Empty);

        var allFiles = new[] { ".gitignore", "file.txt", "file.TXT" };
        var expected = QueryVisiblePathsFromGit(git, allFiles);

        var walker = new FileTreeWalker();
        var context = RepositoryDiscovery.Discover(tempDirectory.Path);
        var actual = walker.Enumerate(tempDirectory.Path, new FileTreeWalkOptions { RepositoryContext = context })
            .Select(static x => x.RelativePath)
            .OrderBy(static x => x, StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(expected, actual, $"Mismatch for core.ignorecase={ignoreCase}.");
    }

    private static void AssertTimestampClose(DateTimeOffset expected, DateTimeOffset actual)
    {
        var delta = (expected - actual).Duration();
        Assert.IsTrue(delta <= TimeSpan.FromSeconds(1), $"Expected timestamp {expected:O} to be within one second of {actual:O}.");
    }
}
