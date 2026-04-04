// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Git;
using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.IO;

/// <summary>
/// Controls file tree traversal behavior.
/// </summary>
/// <remarks>
/// <para><see cref="FileTreeWalkOptions"/> is immutable after construction and may be shared across threads.</para>
/// <para><see cref="FileTreeWalker"/> snapshots the option values and additional rule-set references when enumeration starts, so later changes to any external collection object are not observed by an in-flight traversal.</para>
/// </remarks>
public sealed record FileTreeWalkOptions
{
    /// <summary>
    /// Gets a value indicating whether directory entries should be yielded.
    /// </summary>
    public bool IncludeDirectories { get; init; }

    /// <summary>
    /// Gets a value indicating whether symbolic links and reparse points should be followed.
    /// The default is <see langword="false"/>.
    /// </summary>
    public bool FollowSymbolicLinks { get; init; }

    /// <summary>
    /// Gets the cancellation token used during traversal.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets the repository context used for Git-aware ignore resolution.
    /// </summary>
    public RepositoryContext? RepositoryContext { get; init; }

    /// <summary>
    /// Gets the additional ignore rule sets that participate in traversal.
    /// Later rule sets have higher precedence. Entry ordering is otherwise unspecified.
    /// </summary>
    public IReadOnlyList<IgnoreRuleSet>? AdditionalRuleSets { get; init; }
}
