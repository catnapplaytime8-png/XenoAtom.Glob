// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Git;
using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Internal;

internal sealed class IgnoreStack
{
    public IgnoreStack(IReadOnlyList<IgnoreRuleSet> ruleSets)
    {
        RuleSets = ruleSets;
        Matcher = new IgnoreMatcher(ruleSets);
    }

    public IReadOnlyList<IgnoreRuleSet> RuleSets { get; }

    public IgnoreMatcher Matcher { get; }

    public IgnoreStack PushDirectory(RepositoryContext? repositoryContext, string relativeDirectory)
    {
        if (repositoryContext is null)
        {
            return this;
        }

        var childRuleSets = repositoryContext.CreateChildRuleSets(RuleSets, relativeDirectory);
        return ReferenceEquals(childRuleSets, RuleSets) ? this : new IgnoreStack(childRuleSets);
    }
}
