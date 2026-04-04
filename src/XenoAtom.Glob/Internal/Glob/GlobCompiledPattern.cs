// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;

namespace XenoAtom.Glob.Internal;

internal sealed class GlobCompiledPattern
{
    public GlobCompiledPattern(
        GlobCompiledSegment[] segments,
        bool hasLeadingSeparator,
        bool hasTrailingSeparator,
        GlobPatternKind kind,
        string? exactText,
        string? prefixText,
        string? suffixText)
    {
        Segments = segments;
        HasLeadingSeparator = hasLeadingSeparator;
        HasTrailingSeparator = hasTrailingSeparator;
        Kind = kind;
        ExactText = exactText;
        PrefixText = prefixText;
        SuffixText = suffixText;
    }

    public GlobCompiledSegment[] Segments { get; }

    public bool HasLeadingSeparator { get; }

    public bool HasTrailingSeparator { get; }

    public GlobPatternKind Kind { get; }

    public string? ExactText { get; }

    public string? PrefixText { get; }

    public string? SuffixText { get; }

    public bool HasTrailingRecursiveWildcard => Segments.Length > 0 && Segments[^1].IsRecursiveWildcard;

    public bool Match(NormalizedPath path, PathStringComparison comparison)
        => Match(path.Value.AsSpan(), path.IsDirectory, path.SegmentCount, comparison, evaluator: null);

    public bool Match(ReadOnlySpan<char> path, bool isDirectory, int segmentCount, PathStringComparison comparison, Ignore.IgnoreMatcherEvaluator? evaluator = null)
    {
        return Kind switch
        {
            GlobPatternKind.Empty => path.IsEmpty,
            GlobPatternKind.Exact => comparison.Equals(path, ExactText!),
            GlobPatternKind.Prefix => segmentCount == 1 && comparison.StartsWith(path, PrefixText!),
            GlobPatternKind.Suffix => segmentCount == 1 && comparison.EndsWith(path, SuffixText!),
            GlobPatternKind.MatchAll => segmentCount == 1,
            GlobPatternKind.RecursiveMatchAll => true,
            _ => MatchGeneral(path, segmentCount, comparison, evaluator),
        };
    }

    public bool MatchWithoutTrailingRecursiveWildcard(ReadOnlySpan<char> path, int segmentCount, PathStringComparison comparison, Ignore.IgnoreMatcherEvaluator? evaluator = null)
    {
        if (!HasTrailingRecursiveWildcard || Segments.Length == 1)
        {
            return false;
        }

        return MatchGeneral(path, segmentCount, comparison, evaluator, Segments.Length - 1);
    }

    public bool MatchGeneralOnly(NormalizedPath path, PathStringComparison comparison)
    {
        return MatchGeneral(path.Value.AsSpan(), path.SegmentCount, comparison, evaluator: null);
    }

    public string GetDebugView() => $"{Kind}: {string.Join("/", Segments.Select(static x => x.RawText))}";

    private bool MatchGeneral(ReadOnlySpan<char> path, int segmentCount, PathStringComparison comparison, Ignore.IgnoreMatcherEvaluator? evaluator, int? patternLength = null)
    {
        var maxPatternLength = patternLength ?? Segments.Length;
        if (segmentCount <= 32)
        {
            Span<SegmentRange> segmentRanges = stackalloc SegmentRange[segmentCount];
            FillPathSegments(path, segmentRanges);
            return MatchSegments(path, segmentRanges, 0, 0, maxPatternLength, comparison);
        }

        if (evaluator is not null)
        {
            var segmentRanges = evaluator.GetSegmentBuffer(segmentCount);
            FillPathSegments(path, segmentRanges);
            return MatchSegments(path, segmentRanges, 0, 0, maxPatternLength, comparison);
        }

        var rentedBuffer = ArrayPool<SegmentRange>.Shared.Rent(segmentCount);
        try
        {
            var segmentRanges = rentedBuffer.AsSpan(0, segmentCount);
            FillPathSegments(path, segmentRanges);
            return MatchSegments(path, segmentRanges, 0, 0, maxPatternLength, comparison);
        }
        finally
        {
            ArrayPool<SegmentRange>.Shared.Return(rentedBuffer);
        }
    }

    private bool MatchSegments(ReadOnlySpan<char> path, ReadOnlySpan<SegmentRange> segmentRanges, int patternIndex, int segmentIndex, int patternLength, PathStringComparison comparison)
    {
        while (patternIndex < patternLength)
        {
            var segment = Segments[patternIndex];
            if (segment.IsRecursiveWildcard)
            {
                while (patternIndex + 1 < patternLength && Segments[patternIndex + 1].IsRecursiveWildcard)
                {
                    patternIndex++;
                }

                if (patternIndex == patternLength - 1)
                {
                    return true;
                }

                for (var candidate = segmentIndex; candidate <= segmentRanges.Length; candidate++)
                {
                    if (MatchSegments(path, segmentRanges, patternIndex + 1, candidate, patternLength, comparison))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (segmentIndex >= segmentRanges.Length)
            {
                return false;
            }

            var range = segmentRanges[segmentIndex];
            if (!segment.IsMatch(path.Slice(range.Start, range.Length), comparison))
            {
                return false;
            }

            patternIndex++;
            segmentIndex++;
        }

        return segmentIndex == segmentRanges.Length;
    }

    private static void FillPathSegments(ReadOnlySpan<char> path, Span<SegmentRange> segments)
    {
        if (segments.Length == 0)
        {
            return;
        }

        var segmentIndex = 0;
        var start = 0;
        for (var index = 0; index <= path.Length; index++)
        {
            if (index < path.Length && path[index] != '/')
            {
                continue;
            }

            segments[segmentIndex++] = new SegmentRange(start, index - start);
            start = index + 1;
        }
    }
}
