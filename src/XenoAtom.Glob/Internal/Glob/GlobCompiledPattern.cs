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

    public bool Match(NormalizedPath path)
    {
        return Kind switch
        {
            GlobPatternKind.Empty => path.IsEmpty,
            GlobPatternKind.Exact => string.Equals(path.Value, ExactText, StringComparison.Ordinal),
            GlobPatternKind.Prefix => path.SegmentCount == 1 && path.Value.StartsWith(PrefixText!, StringComparison.Ordinal),
            GlobPatternKind.Suffix => path.SegmentCount == 1 && path.Value.EndsWith(SuffixText!, StringComparison.Ordinal),
            GlobPatternKind.MatchAll => path.SegmentCount == 1,
            GlobPatternKind.RecursiveMatchAll => true,
            _ => MatchGeneral(path),
        };
    }

    private bool MatchGeneral(NormalizedPath path)
    {
        var segmentCount = path.SegmentCount;
        Span<SegmentRange> segmentRanges = segmentCount <= 32
            ? stackalloc SegmentRange[segmentCount]
            : new SegmentRange[segmentCount];

        FillPathSegments(path.Value, segmentRanges);
        return MatchSegments(path.Value, segmentRanges, 0, 0);
    }

    private bool MatchSegments(string path, ReadOnlySpan<SegmentRange> segmentRanges, int patternIndex, int segmentIndex)
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
                    if (MatchSegments(path, segmentRanges, patternIndex + 1, candidate))
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
            if (!segment.IsMatch(path.AsSpan(range.Start, range.Length)))
            {
                return false;
            }

            patternIndex++;
            segmentIndex++;
        }

        return segmentIndex == segmentRanges.Length;
    }

    private static void FillPathSegments(string path, Span<SegmentRange> segments)
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
