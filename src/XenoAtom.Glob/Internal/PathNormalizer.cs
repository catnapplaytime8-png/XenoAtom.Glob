// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;

namespace XenoAtom.Glob.Internal;

internal static class PathNormalizer
{
    private const int StackallocThreshold = 256;

    public static NormalizedPath NormalizeRelativePath(string path, bool isDirectory = false)
    {
        ArgumentNullException.ThrowIfNull(path);

        var result = TryNormalizeRelativePath(path, isDirectory);
        if (result.Success)
        {
            return result.Path;
        }

        throw new ArgumentException(GetErrorMessage(result.Error), nameof(path));
    }

    public static PathNormalizationResult TryNormalizeRelativePath(string path, bool isDirectory = false)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (IsAbsolutePath(path))
        {
            return PathNormalizationResult.Failure(PathNormalizationError.AbsolutePathNotSupported);
        }

        var inferredDirectory = isDirectory || EndsWithSeparator(path);
        if (path.Length == 0)
        {
            return PathNormalizationResult.FromPath(new NormalizedPath(string.Empty, inferredDirectory, segmentCount: 0));
        }

        if (TryReturnUnchangedPath(path, inferredDirectory, out var unchangedResult))
        {
            return unchangedResult;
        }

        char[]? rentedBuffer = null;
        Span<char> destination = path.Length <= StackallocThreshold
            ? stackalloc char[path.Length]
            : (rentedBuffer = ArrayPool<char>.Shared.Rent(path.Length));

        try
        {
            var written = 0;
            var segmentStart = 0;
            var segmentLength = 0;

            for (var index = 0; index <= path.Length; index++)
            {
                if (index < path.Length && !IsSeparator(path[index]))
                {
                    if (segmentLength == 0)
                    {
                        segmentStart = index;
                    }

                    segmentLength++;
                    continue;
                }

                if (segmentLength == 0)
                {
                    continue;
                }

                var segment = path.AsSpan(segmentStart, segmentLength);
                if (segment.SequenceEqual("."))
                {
                    segmentLength = 0;
                    continue;
                }

                if (segment.SequenceEqual(".."))
                {
                    return PathNormalizationResult.Failure(PathNormalizationError.ParentDirectorySegmentsNotSupported);
                }

                if (written > 0)
                {
                    destination[written++] = '/';
                }

                segment.CopyTo(destination[written..]);
                written += segment.Length;
                segmentLength = 0;
            }

            var normalizedValue = written == 0 ? string.Empty : new string(destination[..written]);
            return PathNormalizationResult.FromPath(new NormalizedPath(normalizedValue, inferredDirectory, CountSegments(normalizedValue)));
        }
        finally
        {
            if (rentedBuffer is not null)
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }

    private static bool TryReturnUnchangedPath(string path, bool inferredDirectory, out PathNormalizationResult result)
    {
        if (EndsWithSeparator(path))
        {
            result = default;
            return false;
        }

        var segmentLength = 0;
        for (var index = 0; index <= path.Length; index++)
        {
            if (index < path.Length && !IsSeparator(path[index]))
            {
                segmentLength++;
                continue;
            }

            if (segmentLength == 0)
            {
                result = default;
                return false;
            }

            var segment = path.AsSpan(index - segmentLength, segmentLength);
            if (segment.SequenceEqual("."))
            {
                result = default;
                return false;
            }

            if (segment.SequenceEqual(".."))
            {
                result = PathNormalizationResult.Failure(PathNormalizationError.ParentDirectorySegmentsNotSupported);
                return true;
            }

            if (index < path.Length && path[index] == '\\')
            {
                result = default;
                return false;
            }

            segmentLength = 0;
        }

        result = PathNormalizationResult.FromPath(new NormalizedPath(path, inferredDirectory, CountSegments(path)));
        return true;
    }

    private static int CountSegments(ReadOnlySpan<char> path)
    {
        if (path.Length == 0)
        {
            return 0;
        }

        var count = 1;
        for (var index = 0; index < path.Length; index++)
        {
            if (path[index] == '/')
            {
                count++;
            }
        }

        return count;
    }

    private static string GetErrorMessage(PathNormalizationError error) => error switch
    {
        PathNormalizationError.AbsolutePathNotSupported => "Only relative paths are supported.",
        PathNormalizationError.ParentDirectorySegmentsNotSupported => "Parent directory segments ('..') are not supported.",
        _ => "The path could not be normalized.",
    };

    private static bool EndsWithSeparator(string path) => path.Length > 0 && IsSeparator(path[^1]);

    private static bool IsSeparator(char c) => c is '/' or '\\';

    private static bool IsAbsolutePath(string path)
    {
        if (path.Length == 0)
        {
            return false;
        }

        if (IsSeparator(path[0]))
        {
            return true;
        }

        return path.Length >= 2 && path[1] == ':' && char.IsAsciiLetter(path[0]);
    }
}
