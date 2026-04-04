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
    private const int IndexThreshold = 32;
    private const int MinimumIndexedRuleCount = 16;

    private readonly IndexedRuleSet[] _indexedRuleSets;
    private readonly PathStringComparison _comparison;
    private readonly bool _ignoreCase;

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
        _comparison = comparison;
        _ignoreCase = comparison.Value == StringComparison.OrdinalIgnoreCase;
        _indexedRuleSets = BuildIndexedRuleSets(ruleSets, _ignoreCase);
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

            var directoryDecision = EvaluateSinglePath(normalizedPath[..index], isDirectory: true, trace: null);
            if (directoryDecision.IsMatch && directoryDecision.IsIgnored)
            {
                return directoryDecision;
            }
        }

        return EvaluateSinglePath(normalizedPath, isDirectory, trace: null);
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
        var path = normalizedPath.Value.AsSpan();
        for (var index = 0; index < path.Length; index++)
        {
            if (path[index] != '/')
            {
                continue;
            }

            var directoryDecision = EvaluateSinglePath(path[..index], isDirectory: true, trace);
            if (directoryDecision.IsMatch && directoryDecision.IsIgnored)
            {
                return new IgnoreEvaluationTrace(directoryDecision, trace ?? []);
            }
        }

        return new IgnoreEvaluationTrace(EvaluateSinglePath(path, normalizedPath.IsDirectory, trace), trace ?? []);
    }

    private IgnoreEvaluationResult EvaluateSinglePath(ReadOnlySpan<char> candidatePath, bool isDirectory, List<IgnoreRule>? trace)
    {
        IgnoreRule? winningRule = null;
        var ignored = false;

        for (var ruleSetIndex = 0; ruleSetIndex < _indexedRuleSets.Length; ruleSetIndex++)
        {
            EvaluateRuleSet(_indexedRuleSets[ruleSetIndex], candidatePath, isDirectory, trace, ref winningRule, ref ignored);
        }

        return winningRule is null
            ? default
            : new IgnoreEvaluationResult(true, ignored, winningRule);
    }

    private void EvaluateRuleSet(
        IndexedRuleSet indexedRuleSet,
        ReadOnlySpan<char> candidatePath,
        bool isDirectory,
        List<IgnoreRule>? trace,
        ref IgnoreRule? winningRule,
        ref bool ignored)
    {
        if (!indexedRuleSet.HasIndex)
        {
            for (var ruleIndex = 0; ruleIndex < indexedRuleSet.Rules.Count; ruleIndex++)
            {
                var rule = indexedRuleSet.Rules[ruleIndex];
                if (!IgnoreRuleMatcher.IsMatch(rule, candidatePath, isDirectory, _comparison))
                {
                    continue;
                }

                trace?.Add(rule);
                winningRule = rule;
                ignored = !rule.IsNegated;
            }

            return;
        }

        var finalSegment = GetFinalSegment(candidatePath);
        var exactCandidates = finalSegment.Length == 0
            ? []
            : GetCandidates(indexedRuleSet.ExactBasenameRules, finalSegment);
        var extension = GetExtension(finalSegment);
        var suffixCandidates = extension.Length == 0
            ? []
            : GetCandidates(indexedRuleSet.ExtensionSuffixRules, extension);

        if (indexedRuleSet.FallbackRuleIndices.Length == 0 &&
            exactCandidates.Length == 0 &&
            suffixCandidates.Length == 0)
        {
            return;
        }

        var fallbackIndex = 0;
        var exactIndex = 0;
        var suffixIndex = 0;
        var previousRuleIndex = -1;

        while (true)
        {
            var nextFallback = fallbackIndex < indexedRuleSet.FallbackRuleIndices.Length ? indexedRuleSet.FallbackRuleIndices[fallbackIndex] : int.MaxValue;
            var nextExact = exactIndex < exactCandidates.Length ? exactCandidates[exactIndex].RuleIndex : int.MaxValue;
            var nextSuffix = suffixIndex < suffixCandidates.Length ? suffixCandidates[suffixIndex].RuleIndex : int.MaxValue;
            var nextRuleIndex = Math.Min(nextFallback, Math.Min(nextExact, nextSuffix));
            if (nextRuleIndex == int.MaxValue)
            {
                break;
            }

            if (nextFallback == nextRuleIndex)
            {
                fallbackIndex++;
            }

            if (nextExact == nextRuleIndex)
            {
                exactIndex++;
            }

            if (nextSuffix == nextRuleIndex)
            {
                suffixIndex++;
            }

            if (nextRuleIndex == previousRuleIndex)
            {
                continue;
            }

            previousRuleIndex = nextRuleIndex;
            var rule = indexedRuleSet.Rules[nextRuleIndex];
            if (!IgnoreRuleMatcher.IsMatch(rule, candidatePath, isDirectory, _comparison))
            {
                continue;
            }

            trace?.Add(rule);
            winningRule = rule;
            ignored = !rule.IsNegated;
        }
    }

    private static IndexedRuleSet[] BuildIndexedRuleSets(IReadOnlyList<IgnoreRuleSet> ruleSets, bool ignoreCase)
    {
        var indexedRuleSets = new IndexedRuleSet[ruleSets.Count];
        for (var index = 0; index < ruleSets.Count; index++)
        {
            indexedRuleSets[index] = BuildIndexedRuleSet(ruleSets[index], ignoreCase);
        }

        return indexedRuleSets;
    }

    private static IndexedRuleSet BuildIndexedRuleSet(IgnoreRuleSet ruleSet, bool ignoreCase)
    {
        var rules = ruleSet.Rules;
        if (rules.Count < IndexThreshold)
        {
            return IndexedRuleSet.WithoutIndex(rules);
        }

        Dictionary<int, List<IndexedRuleCandidate>>? exactBasenameRules = null;
        Dictionary<int, List<IndexedRuleCandidate>>? extensionSuffixRules = null;
        var fallbackRuleIndices = new List<int>(rules.Count);
        var indexedRuleCount = 0;

        for (var ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
        {
            var rule = rules[ruleIndex];
            if (!rule.BasenameOnly)
            {
                fallbackRuleIndices.Add(ruleIndex);
                continue;
            }

            if (rule.CompiledPattern.Kind == GlobPatternKind.Exact)
            {
                AddCandidate(ref exactBasenameRules, ignoreCase, rule.CompiledPattern.ExactText!, ruleIndex);
                indexedRuleCount++;
                continue;
            }

            if (rule.CompiledPattern.Kind == GlobPatternKind.Suffix &&
                rule.CompiledPattern.SuffixText is { Length: > 0 } suffixText &&
                suffixText[0] == '.')
            {
                AddCandidate(ref extensionSuffixRules, ignoreCase, suffixText, ruleIndex);
                indexedRuleCount++;
                continue;
            }

            fallbackRuleIndices.Add(ruleIndex);
        }

        if (indexedRuleCount < MinimumIndexedRuleCount)
        {
            return IndexedRuleSet.WithoutIndex(rules);
        }

        return IndexedRuleSet.WithIndex(
            rules,
            fallbackRuleIndices.ToArray(),
            ConvertBuckets(exactBasenameRules),
            ConvertBuckets(extensionSuffixRules));
    }

    private static void AddCandidate(
        ref Dictionary<int, List<IndexedRuleCandidate>>? buckets,
        bool ignoreCase,
        string matchText,
        int ruleIndex)
    {
        buckets ??= new Dictionary<int, List<IndexedRuleCandidate>>();

        var hash = ComputeHash(matchText, ignoreCase);
        if (!buckets.TryGetValue(hash, out var candidates))
        {
            candidates = [];
            buckets.Add(hash, candidates);
        }

        candidates.Add(new IndexedRuleCandidate(ruleIndex, matchText));
    }

    private static Dictionary<int, IndexedRuleCandidate[]> ConvertBuckets(Dictionary<int, List<IndexedRuleCandidate>>? buckets)
    {
        if (buckets is null || buckets.Count == 0)
        {
            return EmptyCandidateBuckets.Instance;
        }

        var result = new Dictionary<int, IndexedRuleCandidate[]>(buckets.Count);
        foreach (var pair in buckets)
        {
            result.Add(pair.Key, pair.Value.ToArray());
        }

        return result;
    }

    private static ReadOnlySpan<char> GetFinalSegment(ReadOnlySpan<char> candidatePath)
    {
        var separatorIndex = candidatePath.LastIndexOf('/');
        return separatorIndex >= 0 ? candidatePath[(separatorIndex + 1)..] : candidatePath;
    }

    private static ReadOnlySpan<char> GetExtension(ReadOnlySpan<char> segment)
    {
        var dotIndex = segment.LastIndexOf('.');
        return dotIndex >= 0 ? segment[dotIndex..] : default;
    }

    private IndexedRuleCandidate[] GetCandidates(Dictionary<int, IndexedRuleCandidate[]> buckets, ReadOnlySpan<char> key)
    {
        if (buckets.Count == 0)
        {
            return [];
        }

        var hash = ComputeHash(key, _ignoreCase);
        if (!buckets.TryGetValue(hash, out var candidates))
        {
            return [];
        }

        return candidates;
    }

    private static int ComputeHash(ReadOnlySpan<char> text, bool ignoreCase)
    {
        var hash = new HashCode();
        for (var index = 0; index < text.Length; index++)
        {
            hash.Add(ignoreCase ? char.ToUpperInvariant(text[index]) : text[index]);
        }

        return hash.ToHashCode();
    }

    private sealed class IndexedRuleSet
    {
        private IndexedRuleSet(
            IReadOnlyList<IgnoreRule> rules,
            bool hasIndex,
            int[] fallbackRuleIndices,
            Dictionary<int, IndexedRuleCandidate[]> exactBasenameRules,
            Dictionary<int, IndexedRuleCandidate[]> extensionSuffixRules)
        {
            Rules = rules;
            HasIndex = hasIndex;
            FallbackRuleIndices = fallbackRuleIndices;
            ExactBasenameRules = exactBasenameRules;
            ExtensionSuffixRules = extensionSuffixRules;
        }

        public IReadOnlyList<IgnoreRule> Rules { get; }

        public bool HasIndex { get; }

        public int[] FallbackRuleIndices { get; }

        public Dictionary<int, IndexedRuleCandidate[]> ExactBasenameRules { get; }

        public Dictionary<int, IndexedRuleCandidate[]> ExtensionSuffixRules { get; }

        public static IndexedRuleSet WithoutIndex(IReadOnlyList<IgnoreRule> rules)
            => new(rules, hasIndex: false, [], EmptyCandidateBuckets.Instance, EmptyCandidateBuckets.Instance);

        public static IndexedRuleSet WithIndex(
            IReadOnlyList<IgnoreRule> rules,
            int[] fallbackRuleIndices,
            Dictionary<int, IndexedRuleCandidate[]> exactBasenameRules,
            Dictionary<int, IndexedRuleCandidate[]> extensionSuffixRules)
            => new(rules, hasIndex: true, fallbackRuleIndices, exactBasenameRules, extensionSuffixRules);
    }

    private readonly record struct IndexedRuleCandidate(int RuleIndex, string MatchText);

    private static class EmptyCandidateBuckets
    {
        public static readonly Dictionary<int, IndexedRuleCandidate[]> Instance = new();
    }
}
