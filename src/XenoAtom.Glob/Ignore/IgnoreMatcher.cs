// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.Ignore;

/// <summary>
/// Evaluates layered ignore rule sets against normalized relative paths.
/// </summary>
public sealed class IgnoreMatcher
{
    private readonly IReadOnlyList<IgnoreRuleSet> _ruleSets;

    /// <summary>
    /// Initializes a new instance of the <see cref="IgnoreMatcher"/> class.
    /// Later rule sets have higher precedence than earlier ones.
    /// </summary>
    /// <param name="ruleSets">The rule sets to evaluate in precedence order.</param>
    public IgnoreMatcher(params IgnoreRuleSet[] ruleSets)
        : this((IReadOnlyList<IgnoreRuleSet>)ruleSets)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IgnoreMatcher"/> class.
    /// Later rule sets have higher precedence than earlier ones.
    /// </summary>
    /// <param name="ruleSets">The rule sets to evaluate in precedence order.</param>
    public IgnoreMatcher(IReadOnlyList<IgnoreRuleSet> ruleSets)
    {
        ArgumentNullException.ThrowIfNull(ruleSets);
        _ruleSets = ruleSets;
    }

    /// <summary>
    /// Evaluates ignore rules for the specified relative path.
    /// </summary>
    /// <param name="path">The relative path to evaluate.</param>
    /// <param name="isDirectory">A value indicating whether the path is a directory.</param>
    /// <returns>The ignore evaluation result.</returns>
    public IgnoreEvaluationResult Evaluate(string path, bool isDirectory = false)
    {
        var normalizedPath = PathNormalizer.NormalizeRelativePath(path, isDirectory);
        return Evaluate(normalizedPath);
    }

    internal IgnoreEvaluationResult Evaluate(NormalizedPath normalizedPath)
    {
        var segments = SplitSegments(normalizedPath.Value);

        for (var depth = 0; depth < segments.Length - (normalizedPath.IsDirectory ? 0 : 1); depth++)
        {
            var directoryPrefix = string.Join('/', segments.Take(depth + 1));
            var directoryDecision = EvaluateSinglePath(directoryPrefix, isDirectory: true);
            if (directoryDecision.IsMatch && directoryDecision.IsIgnored)
            {
                return directoryDecision;
            }
        }

        return EvaluateSinglePath(normalizedPath.Value, normalizedPath.IsDirectory);
    }

    private IgnoreEvaluationResult EvaluateSinglePath(string candidatePath, bool isDirectory)
    {
        IgnoreRule? winningRule = null;
        var ignored = false;

        foreach (var ruleSet in _ruleSets)
        {
            foreach (var rule in ruleSet.Rules)
            {
                if (!IgnoreRuleMatcher.IsMatch(rule, candidatePath, isDirectory))
                {
                    continue;
                }

                winningRule = rule;
                ignored = !rule.IsNegated;
            }
        }

        return winningRule is null
            ? default
            : new IgnoreEvaluationResult(true, ignored, winningRule);
    }

    private static string[] SplitSegments(string path)
    {
        if (path.Length == 0)
        {
            return [];
        }

        return path.Split('/');
    }
}
