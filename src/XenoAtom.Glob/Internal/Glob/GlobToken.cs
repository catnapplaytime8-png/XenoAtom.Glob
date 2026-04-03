// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Internal;

internal readonly record struct GlobToken(GlobTokenKind Kind, string? Literal, GlobCharClass? CharClass)
{
    public static GlobToken LiteralToken(string value) => new(GlobTokenKind.Literal, value, null);

    public static GlobToken StarToken => new(GlobTokenKind.Star, null, null);

    public static GlobToken QuestionToken => new(GlobTokenKind.Question, null, null);

    public static GlobToken CharClassToken(GlobCharClass charClass) => new(GlobTokenKind.CharClass, null, charClass);
}
