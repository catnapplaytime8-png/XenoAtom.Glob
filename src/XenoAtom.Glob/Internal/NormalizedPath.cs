// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Internal;

internal readonly record struct NormalizedPath(string Value, bool IsDirectory)
{
    public bool IsEmpty => Value.Length == 0;

    public int SegmentCount
    {
        get
        {
            if (Value.Length == 0)
            {
                return 0;
            }

            var count = 1;
            foreach (var c in Value)
            {
                if (c == '/')
                {
                    count++;
                }
            }

            return count;
        }
    }

    public PathSegmentEnumerator EnumerateSegments() => new(Value);

    public override string ToString() => IsDirectory ? $"{Value}/" : Value;
}
