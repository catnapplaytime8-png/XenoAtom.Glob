// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Tests.TestInfrastructure;

namespace XenoAtom.Glob.Tests.Internal;

[TestClass]
public class GitCliTests
{
    [TestMethod]
    public void Version_ShouldBeAvailable()
    {
        StringAssert.StartsWith(GitCli.Version, "git version ");
    }

    [TestMethod]
    public void RunChecked_ShouldExecuteGitCommands()
    {
        using var tempDirectory = new TemporaryDirectory();
        var git = GitCli.In(tempDirectory.Path);
        git.RunChecked("init", "--quiet");

        var result = git.RunChecked("rev-parse", "--is-inside-work-tree");

        Assert.AreEqual("true\n", result.StandardOutput);
    }
}
