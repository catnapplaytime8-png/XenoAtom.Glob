// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Internal;

internal static class IgnoreRuleMatcher
{
    public static bool IsMatch(IgnoreRule rule, string candidatePath, int candidateLength, bool isDirectory, PathStringComparison comparison)
        => IsMatch(rule, candidatePath.AsSpan(0, candidateLength), isDirectory, comparison, evaluator: null);

    public static bool IsMatch(IgnoreRule rule, ReadOnlySpan<char> candidatePath, bool isDirectory, PathStringComparison comparison, IgnoreMatcherEvaluator? evaluator = null)
    {
        if (!TryGetRelativePathRange(rule.BaseDirectory, candidatePath, comparison, out var relativeStart, out var relativeLength))
        {
            return false;
        }

        if (relativeLength == 0)
        {
            return false;
        }

        var relativePath = candidatePath.Slice(relativeStart, relativeLength);
        if (rule.BasenameOnly)
        {
            var lastSeparator = relativePath.LastIndexOf('/');
            var finalSegment = lastSeparator >= 0 ? relativePath[(lastSeparator + 1)..] : relativePath;
            if (finalSegment.Length == 0)
            {
                return false;
            }

            if (rule.DirectoryOnly && !isDirectory)
            {
                return false;
            }

            return rule.CompiledPattern.Match(finalSegment, isDirectory, segmentCount: 1, comparison, evaluator);
        }

        if (rule.DirectoryOnly && !isDirectory)
        {
            return false;
        }

        var segmentCount = CountSegments(relativePath);
        if (!rule.CompiledPattern.Match(relativePath, isDirectory, segmentCount, comparison, evaluator))
        {
            return false;
        }

        return !MatchesBoundaryDirectoryOnly(rule, relativePath, segmentCount, comparison, evaluator);
    }

    private static bool TryGetRelativePathRange(string baseDirectory, ReadOnlySpan<char> candidatePath, PathStringComparison comparison, out int relativeStart, out int relativeLength)
    {
        if (baseDirectory.Length == 0)
        {
            relativeStart = 0;
            relativeLength = candidatePath.Length;
            return true;
        }

        if (candidatePath.Length == baseDirectory.Length &&
            comparison.Equals(candidatePath, baseDirectory))
        {
            relativeStart = 0;
            relativeLength = 0;
            return true;
        }

        if (candidatePath.Length > baseDirectory.Length &&
            comparison.StartsWith(candidatePath, baseDirectory) &&
            candidatePath[baseDirectory.Length] == '/')
        {
            relativeStart = baseDirectory.Length + 1;
            relativeLength = candidatePath.Length - relativeStart;
            return true;
        }

        relativeStart = 0;
        relativeLength = 0;
        return false;
    }

    private static int CountSegments(ReadOnlySpan<char> path)
    {
        var segmentCount = 1;
        for (var index = 0; index < path.Length; index++)
        {
            if (path[index] == '/')
            {
                segmentCount++;
            }
        }

        return segmentCount;
    }

    private static bool MatchesBoundaryDirectoryOnly(
        IgnoreRule rule,
        ReadOnlySpan<char> relativePath,
        int segmentCount,
        PathStringComparison comparison,
        IgnoreMatcherEvaluator? evaluator)
    {
        return rule.CompiledPattern.HasTrailingRecursiveWildcard &&
               rule.CompiledPattern.MatchWithoutTrailingRecursiveWildcard(relativePath, segmentCount, comparison, evaluator);
    }
}
