// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Internal;

internal sealed class GlobCharClass
{
    public GlobCharClass(bool isNegated, GlobCharClassRange[] ranges)
    {
        IsNegated = isNegated;
        Ranges = ranges;
    }

    public bool IsNegated { get; }

    public GlobCharClassRange[] Ranges { get; }

    public bool IsMatch(char value, PathStringComparison comparison)
    {
        var matched = false;
        foreach (var range in Ranges)
        {
            if (range.Contains(value, comparison))
            {
                matched = true;
                break;
            }
        }

        return IsNegated ? !matched : matched;
    }
}
