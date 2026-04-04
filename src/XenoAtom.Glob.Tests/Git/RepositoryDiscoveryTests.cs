// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Git;
using XenoAtom.Glob.Internal;
using XenoAtom.Glob.Tests.TestInfrastructure;

namespace XenoAtom.Glob.Tests.Git;

[TestClass]
public class RepositoryDiscoveryTests
{
    [TestMethod]
    public void Discover_ShouldResolveStandardGitDirectory()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        var context = RepositoryDiscovery.Discover(tempDirectory.Path);

        Assert.AreEqual(Path.GetFullPath(tempDirectory.Path), context.WorkingTreeRoot);
        Assert.AreEqual(Path.Combine(Path.GetFullPath(tempDirectory.Path), ".git"), context.GitDirectory);
    }

    [TestMethod]
    public void Discover_ShouldResolveGitFile()
    {
        using var tempDirectory = new TemporaryDirectory();
        tempDirectory.CreateDirectory(".realgit", "info");
        File.WriteAllText(tempDirectory.GetPath(".git"), "gitdir: .realgit\n");
        File.WriteAllText(tempDirectory.GetPath(".realgit", "config"), "[core]\n\trepositoryformatversion = 0\n");

        var context = RepositoryDiscovery.Discover(tempDirectory.Path);

        Assert.AreEqual(Path.GetFullPath(tempDirectory.Path), context.WorkingTreeRoot);
        Assert.AreEqual(tempDirectory.GetPath(".realgit"), context.GitDirectory);
    }

    [TestMethod]
    public void Discover_ShouldResolveCoreExcludesFileFromConfig()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        var globalIgnorePath = tempDirectory.GetPath("custom-ignore.txt");
        File.WriteAllText(globalIgnorePath, "*.cache\n");
        File.AppendAllText(tempDirectory.GetPath(".git", "config"), $"[core]{Environment.NewLine}\texcludesFile = {globalIgnorePath}{Environment.NewLine}");

        var context = RepositoryDiscovery.Discover(tempDirectory.Path);

        Assert.AreEqual(Path.GetFullPath(globalIgnorePath), context.GlobalExcludePath);
    }

    [TestMethod]
    public void Discover_ShouldResolveQuotedCoreExcludesFileAfterSubsectionEntries()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        var globalIgnorePath = tempDirectory.GetPath("global ignore.txt");
        File.WriteAllText(globalIgnorePath, "*.cache\n");
        var escapedPath = globalIgnorePath.Replace("\\", "\\\\", StringComparison.Ordinal);
        File.AppendAllText(
            tempDirectory.GetPath(".git", "config"),
            $"[includeIf \"gitdir:~/work/\"]{Environment.NewLine}\tpath = ignored.cfg{Environment.NewLine}[core]{Environment.NewLine}\texcludesFile = \"{escapedPath}\"{Environment.NewLine}");

        var context = RepositoryDiscovery.Discover(tempDirectory.Path);

        Assert.AreEqual(Path.GetFullPath(globalIgnorePath), context.GlobalExcludePath);
    }

    [TestMethod]
    public void Discover_ShouldExpandHomeInCoreExcludesFile()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.IsFalse(string.IsNullOrEmpty(home));

        var uniqueSegment = $".xenoatom-glob-tests/{Guid.NewGuid():N}/global ignore.txt";
        var fullGlobalIgnorePath = Path.Combine(home, uniqueSegment.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullGlobalIgnorePath)!);

        try
        {
            File.WriteAllText(fullGlobalIgnorePath, "*.cache\n");
            File.AppendAllText(
                tempDirectory.GetPath(".git", "config"),
                $"[core]{Environment.NewLine}\texcludesFile = \"~/{uniqueSegment}\"{Environment.NewLine}");

            var context = RepositoryDiscovery.Discover(tempDirectory.Path);

            Assert.AreEqual(Path.GetFullPath(fullGlobalIgnorePath), context.GlobalExcludePath);
        }
        finally
        {
            var directory = Path.GetDirectoryName(fullGlobalIgnorePath)!;
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void Discover_ShouldResolveCoreIgnoreCaseFromConfig()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");
        git.RunChecked("config", "core.ignorecase", "false");

        var context = RepositoryDiscovery.Discover(tempDirectory.Path);

        Assert.AreEqual(PathStringComparison.Ordinal, context.PathComparison);
    }

    [TestMethod]
    public void Discover_ShouldFallbackToGitCompatiblePlatformDefaultWhenCoreIgnoreCaseIsUnset()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");
        tempDirectory.WriteAllText(".gitignore", "*.TXT\n");

        var context = RepositoryDiscovery.Discover(tempDirectory.Path);
        var fileTxtResult = git.Run("check-ignore", "--no-index", "file.txt");
        var expectedComparison = fileTxtResult.ExitCode == 0
            ? PathStringComparison.OrdinalIgnoreCase
            : PathStringComparison.Ordinal;

        Assert.AreEqual(expectedComparison, context.PathComparison);
    }

    [TestMethod]
    public void Discover_ShouldMatchGitRevParse()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");
        tempDirectory.CreateDirectory("src", "nested");

        var context = RepositoryDiscovery.Discover(tempDirectory.GetPath("src", "nested"));
        var topLevel = git.RunChecked("-C", tempDirectory.GetPath("src", "nested"), "rev-parse", "--show-toplevel").StandardOutput.Trim();
        var gitDir = git.RunChecked("-C", tempDirectory.GetPath("src", "nested"), "rev-parse", "--git-dir").StandardOutput.Trim();
        var resolvedGitDir = Path.GetFullPath(Path.Combine(tempDirectory.GetPath("src", "nested"), gitDir));

        Assert.AreEqual(Path.GetFullPath(topLevel), context.WorkingTreeRoot);
        Assert.AreEqual(Path.GetFullPath(resolvedGitDir), context.GitDirectory);
    }

    [TestMethod]
    public void Discover_ShouldThrowForMalformedGitFile()
    {
        using var tempDirectory = new TemporaryDirectory();
        File.WriteAllText(tempDirectory.GetPath(".git"), "not-a-gitdir\n");

        Assert.Throws<InvalidOperationException>(() => RepositoryDiscovery.Discover(tempDirectory.Path));
    }

    [TestMethod]
    public void Discover_ShouldResolveSubmoduleStyleGitFile()
    {
        using var tempDirectory = new TemporaryDirectory();
        tempDirectory.CreateDirectory(".git", "modules", "submodule", "info");
        File.WriteAllText(tempDirectory.GetPath(".git", "modules", "submodule", "config"), "[core]\n\trepositoryformatversion = 0\n");
        tempDirectory.CreateDirectory("submodule");
        File.WriteAllText(tempDirectory.GetPath("submodule", ".git"), "gitdir: ../.git/modules/submodule\n");

        var context = RepositoryDiscovery.Discover(tempDirectory.GetPath("submodule"));

        Assert.AreEqual(Path.GetFullPath(tempDirectory.GetPath("submodule")), context.WorkingTreeRoot);
        Assert.AreEqual(Path.GetFullPath(tempDirectory.GetPath(".git", "modules", "submodule")), context.GitDirectory);
    }
}
