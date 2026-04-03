// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

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

    public bool Match(NormalizedPath path, PathStringComparison comparison)
        => Match(path.Value.AsSpan(), path.IsDirectory, path.SegmentCount, comparison);

    public bool Match(ReadOnlySpan<char> path, bool isDirectory, int segmentCount, PathStringComparison comparison)
    {
        return Kind switch
        {
            GlobPatternKind.Empty => path.IsEmpty,
            GlobPatternKind.Exact => comparison.Equals(path, ExactText!),
            GlobPatternKind.Prefix => segmentCount == 1 && comparison.StartsWith(path, PrefixText!),
            GlobPatternKind.Suffix => segmentCount == 1 && comparison.EndsWith(path, SuffixText!),
            GlobPatternKind.MatchAll => segmentCount == 1,
            GlobPatternKind.RecursiveMatchAll => true,
            _ => MatchGeneral(path, segmentCount, comparison),
        };
    }

    public bool MatchGeneralOnly(NormalizedPath path, PathStringComparison comparison)
    {
        return MatchGeneral(path.Value.AsSpan(), path.SegmentCount, comparison);
    }

    public string GetDebugView() => $"{Kind}: {string.Join("/", Segments.Select(static x => x.RawText))}";

    private bool MatchGeneral(ReadOnlySpan<char> path, int segmentCount, PathStringComparison comparison)
    {
        Span<SegmentRange> segmentRanges = segmentCount <= 32
            ? stackalloc SegmentRange[segmentCount]
            : new SegmentRange[segmentCount];

        FillPathSegments(path, segmentRanges);
        return MatchSegments(path, segmentRanges, 0, 0, comparison);
    }

    private bool MatchSegments(ReadOnlySpan<char> path, ReadOnlySpan<SegmentRange> segmentRanges, int patternIndex, int segmentIndex, PathStringComparison comparison)
    {
        while (patternIndex < Segments.Length)
        {
            var segment = Segments[patternIndex];
            if (segment.IsRecursiveWildcard)
            {
                while (patternIndex + 1 < Segments.Length && Segments[patternIndex + 1].IsRecursiveWildcard)
                {
                    patternIndex++;
                }

                if (patternIndex == Segments.Length - 1)
                {
                    return true;
                }

                for (var candidate = segmentIndex; candidate <= segmentRanges.Length; candidate++)
                {
                    if (MatchSegments(path, segmentRanges, patternIndex + 1, candidate, comparison))
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
