// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Internal;

internal static class IgnoreRuleMatcher
{
    public static bool IsMatch(IgnoreRule rule, string candidatePath, int candidateLength, bool isDirectory, PathStringComparison comparison)
    {
        if (!TryGetRelativePathRange(rule.BaseDirectory, candidatePath, candidateLength, comparison, out var relativeStart, out var relativeLength))
        {
            return false;
        }

        if (relativeLength == 0)
        {
            return false;
        }

        var relativePath = candidatePath.AsSpan(relativeStart, relativeLength);
        if (rule.BasenameOnly)
        {
            var segmentStart = 0;
            for (var index = 0; index <= relativePath.Length; index++)
            {
                if (index < relativePath.Length && relativePath[index] != '/')
                {
                    continue;
                }

                var segmentIsDirectory = index < relativePath.Length || isDirectory;
                if (rule.DirectoryOnly && !segmentIsDirectory)
                {
                    segmentStart = index + 1;
                    continue;
                }

                if (rule.CompiledPattern.Match(relativePath.Slice(segmentStart, index - segmentStart), segmentIsDirectory, segmentCount: 1, comparison))
                {
                    return true;
                }

                segmentStart = index + 1;
            }

            return false;
        }

        var matchedSegmentCount = 0;
        for (var index = 0; index <= relativePath.Length; index++)
        {
            if (index < relativePath.Length && relativePath[index] != '/')
            {
                continue;
            }

            matchedSegmentCount++;
            var prefixIsDirectory = index < relativePath.Length || isDirectory;
            if (rule.DirectoryOnly && !prefixIsDirectory)
            {
                continue;
            }

            if (rule.CompiledPattern.Match(relativePath[..index], prefixIsDirectory, matchedSegmentCount, comparison))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetRelativePathRange(string baseDirectory, string candidatePath, int candidateLength, PathStringComparison comparison, out int relativeStart, out int relativeLength)
    {
        if (baseDirectory.Length == 0)
        {
            relativeStart = 0;
            relativeLength = candidateLength;
            return true;
        }

        if (candidateLength == baseDirectory.Length &&
            comparison.Equals(candidatePath.AsSpan(0, candidateLength), baseDirectory))
        {
            relativeStart = 0;
            relativeLength = 0;
            return true;
        }

        if (candidateLength > baseDirectory.Length &&
            comparison.StartsWith(candidatePath.AsSpan(0, candidateLength), baseDirectory) &&
            candidatePath[baseDirectory.Length] == '/')
        {
            relativeStart = baseDirectory.Length + 1;
            relativeLength = candidateLength - relativeStart;
            return true;
        }

        relativeStart = 0;
        relativeLength = 0;
        return false;
    }
}
