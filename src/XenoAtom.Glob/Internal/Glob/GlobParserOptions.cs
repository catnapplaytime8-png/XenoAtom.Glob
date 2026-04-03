// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Internal;

internal readonly record struct GlobParserOptions(bool AllowLeadingSeparator, bool AllowTrailingSeparator)
{
    public static GlobParserOptions Default => new(false, false);

    public static GlobParserOptions IgnorePattern => new(true, true);
}
