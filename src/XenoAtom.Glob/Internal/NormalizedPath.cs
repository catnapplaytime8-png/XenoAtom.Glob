// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Internal;

internal readonly record struct NormalizedPath
{
    public NormalizedPath(string value, bool isDirectory, int segmentCount)
    {
        Value = value;
        IsDirectory = isDirectory;
        SegmentCount = segmentCount;
    }

    public string Value { get; }

    public bool IsDirectory { get; }

    public int SegmentCount { get; }

    public bool IsEmpty => Value.Length == 0;

    public PathSegmentEnumerator EnumerateSegments() => new(Value);

    public override string ToString() => IsDirectory ? $"{Value}/" : Value;
}
