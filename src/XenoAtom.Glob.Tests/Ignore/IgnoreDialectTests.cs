// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Tests.Ignore;

[TestClass]
public class IgnoreDialectTests
{
    [TestMethod]
    public void IgnoreFileDialect_ShouldBehaveLikeCurrentIgnoreImplementation()
    {
        var gitRules = IgnoreRuleSet.Parse("*.tmp\n", IgnoreDialect.GitIgnore);
        var ignoreRules = IgnoreRuleSet.Parse("*.tmp\n", IgnoreDialect.IgnoreFile);
        var gitMatcher = new IgnoreMatcher(gitRules);
        var ignoreMatcher = new IgnoreMatcher(ignoreRules);

        Assert.AreEqual(gitMatcher.Evaluate("file.tmp").IsIgnored, ignoreMatcher.Evaluate("file.tmp").IsIgnored);
    }

    [TestMethod]
    public void IgnoreFileDialect_ShouldNotChangeGitIgnoreSemantics()
    {
        var gitMatcher = new IgnoreMatcher(IgnoreRuleSet.ParseGitIgnore("""
            cache/
            !cache/include.txt
            """));

        var ignoreMatcher = new IgnoreMatcher(IgnoreRuleSet.ParseIgnoreFile("""
            cache/
            !cache/include.txt
            """));

        Assert.AreEqual(gitMatcher.Evaluate("cache/include.txt").IsIgnored, ignoreMatcher.Evaluate("cache/include.txt").IsIgnored);
    }
}
