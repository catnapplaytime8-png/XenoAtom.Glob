// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Internal;

internal readonly record struct GlobParseResult(bool Success, GlobCompiledPattern Pattern, GlobPatternParseError Error)
{
    public static GlobParseResult FromPattern(GlobCompiledPattern pattern) => new(true, pattern, GlobPatternParseError.None);

    public static GlobParseResult Failure(GlobPatternParseError error) => new(false, default!, error);
}
