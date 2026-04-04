// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Git;
using XenoAtom.Glob.IO;
using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Tests;

[TestClass]
public class PublicApiGuardTests
{
    [TestMethod]
    public void GlobPattern_Parse_ShouldThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => GlobPattern.Parse(null!));
    }

    [TestMethod]
    public void IgnoreRuleSet_ParseGitIgnore_ShouldThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => IgnoreRuleSet.ParseGitIgnore((string)null!));
    }

    [TestMethod]
    public void IgnoreMatcherEvaluator_Evaluate_ShouldThrowOnNull()
    {
        using var evaluator = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("*.tmp")).CreateEvaluator();
        Assert.Throws<ArgumentNullException>(() => evaluator.Evaluate((string)null!));
    }

    [TestMethod]
    public void RepositoryDiscovery_Discover_ShouldThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => RepositoryDiscovery.Discover(null!));
    }

    [TestMethod]
    public void FileTreeWalker_Enumerate_ShouldThrowOnNull()
    {
        var walker = new FileTreeWalker();
        Assert.Throws<ArgumentNullException>(() => walker.Enumerate(null!).ToArray());
    }
}
