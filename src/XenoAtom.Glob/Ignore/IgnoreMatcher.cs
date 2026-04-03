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
    private readonly PathStringComparison _comparison;

    /// <summary>
    /// Initializes a new instance of the <see cref="IgnoreMatcher"/> class.
    /// Later rule sets have higher precedence than earlier ones.
    /// </summary>
    /// <param name="ruleSets">The rule sets to evaluate in precedence order.</param>
    public IgnoreMatcher(params IgnoreRuleSet[] ruleSets)
        : this((IReadOnlyList<IgnoreRuleSet>)ruleSets, PathStringComparison.CurrentPlatformDefault)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="IgnoreMatcher"/> class.
    /// Later rule sets have higher precedence than earlier ones.
    /// </summary>
    /// <param name="ruleSets">The rule sets to evaluate in precedence order.</param>
    public IgnoreMatcher(IReadOnlyList<IgnoreRuleSet> ruleSets)
        : this(ruleSets, PathStringComparison.CurrentPlatformDefault)
    {
    }

    internal IgnoreMatcher(IReadOnlyList<IgnoreRuleSet> ruleSets, PathStringComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(ruleSets);
        _ruleSets = ruleSets;
        _comparison = comparison;
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

    internal IgnoreEvaluationResult EvaluateNormalized(ReadOnlySpan<char> normalizedPath, bool isDirectory)
    {
        for (var index = 0; index < normalizedPath.Length; index++)
        {
            if (normalizedPath[index] != '/')
            {
                continue;
            }

            var directoryDecision = EvaluateSinglePath(normalizedPath[..index], isDirectory: true);
            if (directoryDecision.IsMatch && directoryDecision.IsIgnored)
            {
                return directoryDecision;
            }
        }

        return EvaluateSinglePath(normalizedPath, isDirectory);
    }

    internal IgnoreEvaluationResult Evaluate(NormalizedPath normalizedPath)
    {
        return EvaluateInternal(normalizedPath, captureTrace: false).Result;
    }

    internal IgnoreEvaluationTrace EvaluateWithTrace(string path, bool isDirectory = false)
    {
        var normalizedPath = PathNormalizer.NormalizeRelativePath(path, isDirectory);
        return EvaluateInternal(normalizedPath, captureTrace: true);
    }

    internal IgnoreEvaluationTrace EvaluateWithTrace(NormalizedPath normalizedPath)
    {
        return EvaluateInternal(normalizedPath, captureTrace: true);
    }

    private IgnoreEvaluationTrace EvaluateInternal(NormalizedPath normalizedPath, bool captureTrace)
    {
        List<IgnoreRule>? trace = captureTrace ? [] : null;
        var path = normalizedPath.Value;
        for (var index = 0; index < path.Length; index++)
        {
            if (path[index] != '/')
            {
                continue;
            }

            var directoryDecision = EvaluateSinglePath(path, index, isDirectory: true, trace);
            if (directoryDecision.IsMatch && directoryDecision.IsIgnored)
            {
                return new IgnoreEvaluationTrace(directoryDecision, trace ?? []);
            }
        }

        return new IgnoreEvaluationTrace(EvaluateSinglePath(path, path.Length, normalizedPath.IsDirectory, trace), trace ?? []);
    }

    private IgnoreEvaluationResult EvaluateSinglePath(string candidatePath, int candidateLength, bool isDirectory, List<IgnoreRule>? trace)
    {
        IgnoreRule? winningRule = null;
        var ignored = false;

        foreach (var ruleSet in _ruleSets)
        {
            foreach (var rule in ruleSet.Rules)
            {
                if (!IgnoreRuleMatcher.IsMatch(rule, candidatePath, candidateLength, isDirectory, _comparison))
                {
                    continue;
                }

                trace?.Add(rule);
                winningRule = rule;
                ignored = !rule.IsNegated;
            }
        }

        return winningRule is null
            ? default
            : new IgnoreEvaluationResult(true, ignored, winningRule);
    }

    private IgnoreEvaluationResult EvaluateSinglePath(ReadOnlySpan<char> candidatePath, bool isDirectory)
    {
        IgnoreRule? winningRule = null;
        var ignored = false;

        foreach (var ruleSet in _ruleSets)
        {
            foreach (var rule in ruleSet.Rules)
            {
                if (!IgnoreRuleMatcher.IsMatch(rule, candidatePath, isDirectory, _comparison))
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
}
