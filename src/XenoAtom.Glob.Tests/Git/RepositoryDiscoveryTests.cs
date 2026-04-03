// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Git;
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
}
