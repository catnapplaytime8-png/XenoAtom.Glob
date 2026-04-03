// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Ignore;

/// <summary>
/// Identifies where an ignore rule came from.
/// </summary>
public enum IgnoreRuleSourceKind
{
    /// <summary>
    /// A per-directory ignore file such as <c>.gitignore</c>.
    /// </summary>
    PerDirectory,

    /// <summary>
    /// A repository-scoped exclude file such as <c>.git/info/exclude</c>.
    /// </summary>
    RepositoryExclude,

    /// <summary>
    /// A global exclude file such as the path configured by <c>core.excludesFile</c>.
    /// </summary>
    GlobalExclude,

    /// <summary>
    /// A caller-provided rule source with explicit highest precedence.
    /// </summary>
    CommandLine,
}
