// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Git;
using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.IO;

/// <summary>
/// Controls file tree traversal behavior.
/// </summary>
public sealed class FileTreeWalkOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether directory entries should be yielded.
    /// </summary>
    public bool IncludeDirectories { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether symbolic links and reparse points should be followed.
    /// The default is <see langword="false"/>.
    /// </summary>
    public bool FollowSymbolicLinks { get; set; }

    /// <summary>
    /// Gets or sets the repository context used for Git-aware ignore resolution.
    /// </summary>
    public RepositoryContext? RepositoryContext { get; set; }

    /// <summary>
    /// Gets or sets additional ignore rule sets that participate in traversal.
    /// Later rule sets have higher precedence.
    /// </summary>
    public IReadOnlyList<IgnoreRuleSet>? AdditionalRuleSets { get; set; }
}
