// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.Ignore;

/// <summary>
/// Represents one parsed ignore rule.
/// </summary>
public sealed class IgnoreRule
{
    internal IgnoreRule(
        string patternText,
        string rawPatternText,
        bool isNegated,
        bool directoryOnly,
        bool basenameOnly,
        string baseDirectory,
        int lineNumber,
        string? sourcePath,
        IgnoreRuleSourceKind sourceKind,
        GlobCompiledPattern compiledPattern)
    {
        PatternText = patternText;
        RawPatternText = rawPatternText;
        IsNegated = isNegated;
        DirectoryOnly = directoryOnly;
        BasenameOnly = basenameOnly;
        BaseDirectory = baseDirectory;
        LineNumber = lineNumber;
        SourcePath = sourcePath;
        SourceKind = sourceKind;
        CompiledPattern = compiledPattern;
    }

    /// <summary>
    /// Gets the normalized rule pattern without the leading negation marker.
    /// </summary>
    public string PatternText { get; }

    /// <summary>
    /// Gets the raw rule text as written in the ignore file.
    /// </summary>
    public string RawPatternText { get; }

    /// <summary>
    /// Gets a value indicating whether this is a negated rule.
    /// </summary>
    public bool IsNegated { get; }

    /// <summary>
    /// Gets a value indicating whether this rule only matches directories.
    /// </summary>
    public bool DirectoryOnly { get; }

    /// <summary>
    /// Gets a value indicating whether the rule is basename-only.
    /// </summary>
    public bool BasenameOnly { get; }

    /// <summary>
    /// Gets the base directory for this rule, relative to the evaluation root.
    /// </summary>
    public string BaseDirectory { get; }

    /// <summary>
    /// Gets the line number where the rule was defined.
    /// </summary>
    public int LineNumber { get; }

    /// <summary>
    /// Gets the source path that defined the rule, when available.
    /// </summary>
    public string? SourcePath { get; }

    /// <summary>
    /// Gets the kind of source that defined the rule.
    /// </summary>
    public IgnoreRuleSourceKind SourceKind { get; }

    internal GlobCompiledPattern CompiledPattern { get; }
}
