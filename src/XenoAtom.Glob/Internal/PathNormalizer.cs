// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;

namespace XenoAtom.Glob.Internal;

internal static class PathNormalizer
{
    private const int StackallocThreshold = 256;
    private static readonly SearchValues<char> Separators = SearchValues.Create("/\\");

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
            if (!TryNormalizeIntoDestination(path, destination, out var written, out var segmentCount, out var error))
            {
                return PathNormalizationResult.Failure(error);
            }

            var normalizedValue = written == 0 ? string.Empty : new string(destination[..written]);
            return PathNormalizationResult.FromPath(new NormalizedPath(normalizedValue, inferredDirectory, segmentCount));
        }
        finally
        {
            if (rentedBuffer is not null)
            {
                ArrayPool<char>.Shared.Return(rentedBuffer);
            }
        }
    }

    public static bool TryNormalizeRelativePath(
        ReadOnlySpan<char> path,
        bool isDirectory,
        Span<char> destination,
        out ReadOnlySpan<char> normalizedPath,
        out bool normalizedIsDirectory,
        out int segmentCount,
        out PathNormalizationError error)
    {
        if (IsAbsolutePath(path))
        {
            normalizedPath = default;
            normalizedIsDirectory = default;
            segmentCount = 0;
            error = PathNormalizationError.AbsolutePathNotSupported;
            return false;
        }

        normalizedIsDirectory = isDirectory || EndsWithSeparator(path);
        if (path.Length == 0)
        {
            normalizedPath = default;
            segmentCount = 0;
            error = PathNormalizationError.None;
            return true;
        }

        if (TryReturnUnchangedPath(path, out segmentCount, out error))
        {
            if (error != PathNormalizationError.None)
            {
                normalizedPath = default;
                normalizedIsDirectory = default;
                segmentCount = 0;
                return false;
            }

            normalizedPath = path;
            return true;
        }

        if (!TryNormalizeIntoDestination(path, destination, out var written, out segmentCount, out error))
        {
            normalizedPath = default;
            normalizedIsDirectory = default;
            segmentCount = 0;
            return false;
        }

        normalizedPath = destination[..written];
        return true;
    }

    private static bool TryReturnUnchangedPath(string path, bool inferredDirectory, out PathNormalizationResult result)
    {
        if (!TryReturnUnchangedPath(path.AsSpan(), out var segmentCount, out var error))
        {
            result = default;
            return false;
        }

        result = error == PathNormalizationError.None
            ? PathNormalizationResult.FromPath(new NormalizedPath(path, inferredDirectory, segmentCount))
            : PathNormalizationResult.Failure(error);
        return true;
    }

    private static bool TryReturnUnchangedPath(ReadOnlySpan<char> path, out int segmentCount, out PathNormalizationError error)
    {
        if (EndsWithSeparator(path))
        {
            segmentCount = 0;
            error = PathNormalizationError.None;
            return false;
        }

        segmentCount = 0;
        var remaining = path;
        while (!remaining.IsEmpty)
        {
            var separatorIndex = remaining.IndexOfAny(Separators);
            var segment = separatorIndex < 0 ? remaining : remaining[..separatorIndex];
            if (segment.IsEmpty)
            {
                segmentCount = 0;
                error = PathNormalizationError.None;
                return false;
            }

            if (IsCurrentDirectorySegment(segment))
            {
                segmentCount = 0;
                error = PathNormalizationError.None;
                return false;
            }

            if (IsParentDirectorySegment(segment))
            {
                segmentCount = 0;
                error = PathNormalizationError.ParentDirectorySegmentsNotSupported;
                return true;
            }

            segmentCount++;
            if (separatorIndex < 0)
            {
                error = PathNormalizationError.None;
                return true;
            }

            if (remaining[separatorIndex] == '\\')
            {
                segmentCount = 0;
                error = PathNormalizationError.None;
                return false;
            }

            remaining = remaining[(separatorIndex + 1)..];
        }

        error = PathNormalizationError.None;
        return true;
    }

    private static int CountSegments(ReadOnlySpan<char> path)
    {
        if (path.Length == 0)
        {
            return 0;
        }

        var count = 1;
        while (true)
        {
            var separatorIndex = path.IndexOf('/');
            if (separatorIndex < 0)
            {
                return count;
            }

            count++;
            path = path[(separatorIndex + 1)..];
        }
    }

    private static string GetErrorMessage(PathNormalizationError error) => error switch
    {
        PathNormalizationError.AbsolutePathNotSupported => "Only relative paths are supported.",
        PathNormalizationError.ParentDirectorySegmentsNotSupported => "Parent directory segments ('..') are not supported.",
        _ => "The path could not be normalized.",
    };

    private static bool EndsWithSeparator(string path) => path.Length > 0 && IsSeparator(path[^1]);

    private static bool EndsWithSeparator(ReadOnlySpan<char> path) => path.Length > 0 && IsSeparator(path[^1]);

    private static bool IsSeparator(char c) => c is '/' or '\\';

    private static bool TryNormalizeIntoDestination(
        ReadOnlySpan<char> path,
        Span<char> destination,
        out int written,
        out int segmentCount,
        out PathNormalizationError error)
    {
        written = 0;
        segmentCount = 0;
        var remaining = path;

        while (!remaining.IsEmpty)
        {
            var separatorIndex = remaining.IndexOfAny(Separators);
            var segment = separatorIndex < 0 ? remaining : remaining[..separatorIndex];
            if (!segment.IsEmpty)
            {
                if (IsCurrentDirectorySegment(segment))
                {
                    goto NextSegment;
                }

                if (IsParentDirectorySegment(segment))
                {
                    error = PathNormalizationError.ParentDirectorySegmentsNotSupported;
                    return false;
                }

                if (written > 0)
                {
                    destination[written++] = '/';
                }

                segment.CopyTo(destination[written..]);
                written += segment.Length;
                segmentCount++;
            }

        NextSegment:
            if (separatorIndex < 0)
            {
                break;
            }

            remaining = remaining[(separatorIndex + 1)..];
        }

        error = PathNormalizationError.None;
        return true;
    }

    private static bool IsCurrentDirectorySegment(ReadOnlySpan<char> segment) => segment.Length == 1 && segment[0] == '.';

    private static bool IsParentDirectorySegment(ReadOnlySpan<char> segment) => segment.Length == 2 && segment[0] == '.' && segment[1] == '.';

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

    private static bool IsAbsolutePath(ReadOnlySpan<char> path)
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
