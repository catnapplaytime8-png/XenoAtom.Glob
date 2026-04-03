// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Internal;

internal ref struct PathSegmentEnumerator
{
    private readonly ReadOnlySpan<char> _path;
    private int _nextIndex;

    public PathSegmentEnumerator(string path)
    {
        _path = path;
        _nextIndex = 0;
        Current = default;
    }

    public ReadOnlySpan<char> Current { get; private set; }

    public readonly PathSegmentEnumerator GetEnumerator() => this;

    public bool MoveNext()
    {
        if (_nextIndex > _path.Length)
        {
            return false;
        }

        if (_nextIndex == _path.Length)
        {
            _nextIndex++;
            return false;
        }

        var remaining = _path[_nextIndex..];
        var separatorIndex = remaining.IndexOf('/');
        if (separatorIndex < 0)
        {
            Current = remaining;
            _nextIndex = _path.Length;
            return true;
        }

        Current = remaining[..separatorIndex];
        _nextIndex += separatorIndex + 1;
        return true;
    }
}
