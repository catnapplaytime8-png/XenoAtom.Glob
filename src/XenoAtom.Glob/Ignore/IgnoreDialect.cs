// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Ignore;

/// <summary>
/// Identifies the ignore file dialect to parse.
/// </summary>
public enum IgnoreDialect
{
    /// <summary>
    /// Parses rules using Git-compatible <c>.gitignore</c> semantics.
    /// </summary>
    GitIgnore,

    /// <summary>
    /// Parses rules using <c>.ignore</c> semantics.
    /// The current implementation intentionally aligns this dialect with Git-compatible rule syntax.
    /// </summary>
    IgnoreFile,
}
