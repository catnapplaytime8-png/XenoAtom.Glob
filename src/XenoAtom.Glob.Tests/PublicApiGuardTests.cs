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
    public void GlobPattern_TryParse_ShouldThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => GlobPattern.TryParse(null!));
    }

    [TestMethod]
    public void GlobPattern_IsMatch_ShouldThrowOnNull()
    {
        var pattern = GlobPattern.Parse("*.tmp");
        Assert.Throws<ArgumentNullException>(() => pattern.IsMatch((string)null!));
    }

    [TestMethod]
    public void IgnoreRuleSet_ParseGitIgnore_String_ShouldThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => IgnoreRuleSet.ParseGitIgnore((string)null!));
    }

    [TestMethod]
    public void IgnoreRuleSet_ParseGitIgnore_TextReader_ShouldThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => IgnoreRuleSet.ParseGitIgnore((TextReader)null!));
    }

    [TestMethod]
    public void IgnoreRuleSet_ParseGitIgnore_Stream_ShouldThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => IgnoreRuleSet.ParseGitIgnore((Stream)null!));
    }

    [TestMethod]
    public void IgnoreRuleSet_Parse_String_ShouldThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => IgnoreRuleSet.Parse((string)null!, IgnoreDialect.GitIgnore));
    }

    [TestMethod]
    public void IgnoreRuleSet_Parse_TextReader_ShouldThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => IgnoreRuleSet.Parse((TextReader)null!, IgnoreDialect.GitIgnore));
    }

    [TestMethod]
    public void IgnoreRuleSet_Parse_Stream_ShouldThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => IgnoreRuleSet.Parse((Stream)null!, IgnoreDialect.GitIgnore));
    }

    [TestMethod]
    public void IgnoreRuleSet_ParseIgnoreFile_ShouldThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => IgnoreRuleSet.ParseIgnoreFile(null!));
    }

    [TestMethod]
    public void IgnoreMatcher_Evaluate_ShouldThrowOnNull()
    {
        var matcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("*.tmp"));
        Assert.Throws<ArgumentNullException>(() => matcher.Evaluate((string)null!));
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
    public void RepositoryDiscovery_TryDiscover_ShouldThrowOnNull()
    {
        Assert.Throws<ArgumentNullException>(() => RepositoryDiscovery.TryDiscover(null!, out _));
    }

    [TestMethod]
    public void FileTreeWalker_Enumerate_ShouldThrowOnNull()
    {
        var walker = new FileTreeWalker();
        Assert.Throws<ArgumentNullException>(() => walker.Enumerate(null!).ToArray());
    }
}
