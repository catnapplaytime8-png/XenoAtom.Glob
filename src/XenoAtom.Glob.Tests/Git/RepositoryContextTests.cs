// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Git;
using XenoAtom.Glob.Tests.TestInfrastructure;

namespace XenoAtom.Glob.Tests.Git;

[TestClass]
public class RepositoryContextTests
{
    [TestMethod]
    public async Task GetRepositoryIgnoreStack_ShouldCoordinateConcurrentRootGitIgnoreParses()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");
        tempDirectory.WriteAllText(".gitignore", "*.tmp\n");

        var discovered = RepositoryDiscovery.Discover(tempDirectory.Path);
        var gitIgnorePath = tempDirectory.GetPath(".gitignore");
        var readStarted = new ManualResetEventSlim(false);
        var releaseRead = new ManualResetEventSlim(false);
        var readCount = 0;
        var context = new RepositoryContext(
            discovered.WorkingTreeRoot,
            discovered.GitDirectory,
            discovered.GlobalExcludePath,
            discovered.PathComparison,
            path =>
            {
                if (string.Equals(path, gitIgnorePath, StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref readCount);
                    readStarted.Set();
                    releaseRead.Wait(TimeSpan.FromSeconds(5));
                }

                return File.ReadAllText(path);
            });

        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => context.GetRepositoryIgnoreStack(string.Empty)))
            .ToArray();

        try
        {
            Assert.IsTrue(readStarted.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(100).ConfigureAwait(false);
            Assert.AreEqual(1, Volatile.Read(ref readCount));
        }
        finally
        {
            releaseRead.Set();
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        Assert.AreEqual(1, readCount);
    }

    [TestMethod]
    public async Task CreateChildRuleSets_ShouldCoordinateConcurrentNestedGitIgnoreParses()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");
        tempDirectory.WriteAllText(".gitignore", "*.tmp\n");
        tempDirectory.WriteAllText("src/.gitignore", "*.g.cs\n");

        var discovered = RepositoryDiscovery.Discover(tempDirectory.Path);
        var nestedGitIgnorePath = tempDirectory.GetPath("src", ".gitignore");
        var readStarted = new ManualResetEventSlim(false);
        var releaseRead = new ManualResetEventSlim(false);
        var readCount = 0;
        var context = new RepositoryContext(
            discovered.WorkingTreeRoot,
            discovered.GitDirectory,
            discovered.GlobalExcludePath,
            discovered.PathComparison,
            path =>
            {
                if (string.Equals(path, nestedGitIgnorePath, StringComparison.Ordinal))
                {
                    Interlocked.Increment(ref readCount);
                    readStarted.Set();
                    releaseRead.Wait(TimeSpan.FromSeconds(5));
                }

                return File.ReadAllText(path);
            });

        var rootRuleSets = context.CreateInitialRuleSets(string.Empty);
        var expectedRuleSetCount = rootRuleSets.Count + 1;
        var tasks = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => context.CreateChildRuleSets(rootRuleSets, "src")))
            .ToArray();

        try
        {
            Assert.IsTrue(readStarted.Wait(TimeSpan.FromSeconds(5)));
            await Task.Delay(100).ConfigureAwait(false);
            Assert.AreEqual(1, Volatile.Read(ref readCount));
        }
        finally
        {
            releaseRead.Set();
        }

        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        Assert.IsTrue(results.All(x => x.Count == expectedRuleSetCount));
        Assert.AreEqual(1, readCount);
    }
}
