// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob;

/// <summary>
/// Represents the result of parsing a glob pattern.
/// </summary>
public readonly record struct GlobPatternParseResult
{
    internal GlobPatternParseResult(GlobPattern? pattern, GlobPatternParseError error)
    {
        Pattern = pattern;
        Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the parse succeeded.
    /// </summary>
    public bool Success => Pattern is not null;

    /// <summary>
    /// Gets the parsed pattern when the parse succeeded.
    /// </summary>
    public GlobPattern? Pattern { get; }

    /// <summary>
    /// Gets the parse error when the parse failed.
    /// </summary>
    public GlobPatternParseError Error { get; }
}
