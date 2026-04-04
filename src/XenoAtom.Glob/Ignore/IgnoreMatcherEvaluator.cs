// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Buffers;

using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.Ignore;

/// <summary>
/// Reuses temporary buffers for repeated ignore evaluations.
/// </summary>
public sealed class IgnoreMatcherEvaluator : IDisposable
{
    private readonly IgnoreMatcher _matcher;
    private char[] _normalizationBuffer = Array.Empty<char>();
    private SegmentRange[] _segmentBuffer = Array.Empty<SegmentRange>();

    internal IgnoreMatcherEvaluator(IgnoreMatcher matcher)
    {
        _matcher = matcher;
    }

    /// <summary>
    /// Evaluates ignore rules for the specified relative path.
    /// </summary>
    /// <param name="path">The relative path to evaluate.</param>
    /// <param name="isDirectory">A value indicating whether the path is a directory.</param>
    /// <returns>The ignore evaluation result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is not a supported relative path.</exception>
    public IgnoreEvaluationResult Evaluate(string path, bool isDirectory = false)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Evaluate(path.AsSpan(), isDirectory);
    }

    /// <summary>
    /// Evaluates ignore rules for the specified relative path span.
    /// </summary>
    /// <param name="path">The relative path to evaluate.</param>
    /// <param name="isDirectory">A value indicating whether the path is a directory.</param>
    /// <returns>The ignore evaluation result.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is not a supported relative path.</exception>
    public IgnoreEvaluationResult Evaluate(ReadOnlySpan<char> path, bool isDirectory = false)
    {
        var destination = path.Length == 0
            ? Span<char>.Empty
            : EnsureNormalizationBuffer(path.Length).AsSpan(0, path.Length);

        if (!PathNormalizer.TryNormalizeRelativePath(
            path,
            isDirectory,
            destination,
            out var normalizedPath,
            out var normalizedIsDirectory,
            out _,
            out var error))
        {
            throw new ArgumentException(GetPathErrorMessage(error), nameof(path));
        }

        return _matcher.EvaluateNormalized(normalizedPath, normalizedIsDirectory, this);
    }

    /// <summary>
    /// Returns pooled buffers rented by this evaluator.
    /// </summary>
    public void Dispose()
    {
        if (_normalizationBuffer.Length > 0)
        {
            ArrayPool<char>.Shared.Return(_normalizationBuffer);
            _normalizationBuffer = Array.Empty<char>();
        }

        if (_segmentBuffer.Length > 0)
        {
            ArrayPool<SegmentRange>.Shared.Return(_segmentBuffer);
            _segmentBuffer = Array.Empty<SegmentRange>();
        }
    }

    internal Span<SegmentRange> GetSegmentBuffer(int minimumLength)
    {
        if (_segmentBuffer.Length < minimumLength)
        {
            if (_segmentBuffer.Length > 0)
            {
                ArrayPool<SegmentRange>.Shared.Return(_segmentBuffer);
            }

            _segmentBuffer = ArrayPool<SegmentRange>.Shared.Rent(minimumLength);
        }

        return _segmentBuffer.AsSpan(0, minimumLength);
    }

    private char[] EnsureNormalizationBuffer(int minimumLength)
    {
        if (_normalizationBuffer.Length < minimumLength)
        {
            if (_normalizationBuffer.Length > 0)
            {
                ArrayPool<char>.Shared.Return(_normalizationBuffer);
            }

            _normalizationBuffer = ArrayPool<char>.Shared.Rent(minimumLength);
        }

        return _normalizationBuffer;
    }

    private static string GetPathErrorMessage(PathNormalizationError error) => error switch
    {
        PathNormalizationError.AbsolutePathNotSupported => "Only relative paths are supported.",
        PathNormalizationError.ParentDirectorySegmentsNotSupported => "Parent directory segments ('..') are not supported.",
        _ => "The path could not be normalized.",
    };
}
