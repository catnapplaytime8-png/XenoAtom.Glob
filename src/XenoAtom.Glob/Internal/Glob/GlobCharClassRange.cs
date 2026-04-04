// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Internal;

internal readonly record struct GlobCharClassRange(char Start, char End)
{
    public bool Contains(char value) => value >= Start && value <= End;

    public bool Contains(char value, PathStringComparison comparison)
    {
        if (Contains(value))
        {
            return true;
        }

        if (comparison != PathStringComparison.OrdinalIgnoreCase)
        {
            return false;
        }

        var upper = char.ToUpperInvariant(value);
        if (upper != value && Contains(upper))
        {
            return true;
        }

        var lower = char.ToLowerInvariant(value);
        return lower != value && Contains(lower);
    }
}
