// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.Ignore;

/// <summary>
/// Represents an immutable ordered set of ignore rules.
/// </summary>
public sealed class IgnoreRuleSet
{
    private IgnoreRuleSet(IReadOnlyList<IgnoreRule> rules)
    {
        Rules = rules;
    }

    /// <summary>
    /// Gets the parsed rules in source order.
    /// </summary>
    public IReadOnlyList<IgnoreRule> Rules { get; }

    /// <summary>
    /// Parses a Git-compatible ignore file.
    /// </summary>
    /// <param name="content">The ignore file content.</param>
    /// <param name="baseDirectory">The directory containing the ignore file, relative to the evaluation root.</param>
    /// <param name="sourcePath">The optional source path used for diagnostics.</param>
    /// <param name="sourceKind">The rule source kind.</param>
    /// <returns>A parsed ignore rule set.</returns>
    public static IgnoreRuleSet ParseGitIgnore(
        string content,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory)
    {
        ArgumentNullException.ThrowIfNull(content);

        var normalizedBaseDirectory = string.IsNullOrEmpty(baseDirectory)
            ? string.Empty
            : PathNormalizer.NormalizeRelativePath(baseDirectory!, isDirectory: true).Value;

        var rules = GitIgnoreParser.Parse(content, normalizedBaseDirectory, sourcePath, sourceKind);
        return new IgnoreRuleSet(rules);
    }
}
