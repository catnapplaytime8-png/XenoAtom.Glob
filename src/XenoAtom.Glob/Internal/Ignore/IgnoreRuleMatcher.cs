// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Internal;

internal static class IgnoreRuleMatcher
{
    public static bool IsMatch(IgnoreRule rule, string candidatePath, bool isDirectory)
    {
        if (!TryGetRelativePath(rule.BaseDirectory, candidatePath, out var relativePath))
        {
            return false;
        }

        var segments = relativePath.Length == 0 ? [] : relativePath.Split('/');
        if (rule.BasenameOnly)
        {
            for (var index = 0; index < segments.Length; index++)
            {
                var segmentIsDirectory = index < segments.Length - 1 || isDirectory;
                if (rule.DirectoryOnly && !segmentIsDirectory)
                {
                    continue;
                }

                if (rule.CompiledPattern.Match(new NormalizedPath(segments[index], segmentIsDirectory)))
                {
                    return true;
                }
            }

            return false;
        }

        for (var depth = 0; depth < segments.Length; depth++)
        {
            var prefixPath = string.Join('/', segments.Take(depth + 1));
            var prefixIsDirectory = depth < segments.Length - 1 || isDirectory;
            if (rule.DirectoryOnly && !prefixIsDirectory)
            {
                continue;
            }

            if (rule.CompiledPattern.Match(new NormalizedPath(prefixPath, prefixIsDirectory)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetRelativePath(string baseDirectory, string candidatePath, out string relativePath)
    {
        if (baseDirectory.Length == 0)
        {
            relativePath = candidatePath;
            return true;
        }

        if (candidatePath.Length == baseDirectory.Length &&
            string.Equals(candidatePath, baseDirectory, StringComparison.Ordinal))
        {
            relativePath = string.Empty;
            return true;
        }

        if (candidatePath.Length > baseDirectory.Length &&
            candidatePath.StartsWith(baseDirectory, StringComparison.Ordinal) &&
            candidatePath[baseDirectory.Length] == '/')
        {
            relativePath = candidatePath[(baseDirectory.Length + 1)..];
            return true;
        }

        relativePath = string.Empty;
        return false;
    }
}
