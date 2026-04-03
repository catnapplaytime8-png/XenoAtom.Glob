// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Internal;

internal sealed class GlobCompiledSegment
{
    public GlobCompiledSegment(string rawText, GlobToken[] tokens, bool isRecursiveWildcard)
    {
        RawText = rawText;
        Tokens = tokens;
        IsRecursiveWildcard = isRecursiveWildcard;
    }

    public string RawText { get; }

    public GlobToken[] Tokens { get; }

    public bool IsRecursiveWildcard { get; }

    public bool IsLiteral => !IsRecursiveWildcard && Tokens.All(static x => x.Kind == GlobTokenKind.Literal);

    public string? LiteralText => IsLiteral ? string.Concat(Tokens.Select(static x => x.Literal)) : null;

    public bool IsMatch(ReadOnlySpan<char> value, PathStringComparison comparison)
    {
        var tokenIndex = 0;
        var valueIndex = 0;
        var starTokenIndex = -1;
        var starValueIndex = -1;

        while (valueIndex < value.Length)
        {
            if (tokenIndex < Tokens.Length)
            {
                var token = Tokens[tokenIndex];
                switch (token.Kind)
                {
                    case GlobTokenKind.Star:
                        starTokenIndex = tokenIndex++;
                        starValueIndex = valueIndex;
                        continue;

                    case GlobTokenKind.Question:
                        tokenIndex++;
                        valueIndex++;
                        continue;

                    case GlobTokenKind.CharClass:
                        if (token.CharClass!.IsMatch(value[valueIndex]))
                        {
                            tokenIndex++;
                            valueIndex++;
                            continue;
                        }

                        break;

                    case GlobTokenKind.Literal:
                        var literal = token.Literal.AsSpan();
                        if (value[valueIndex..].StartsWith(literal, comparison.Value))
                        {
                            tokenIndex++;
                            valueIndex += literal.Length;
                            continue;
                        }

                        break;
                }
            }

            if (starTokenIndex >= 0)
            {
                tokenIndex = starTokenIndex + 1;
                valueIndex = ++starValueIndex;
                continue;
            }

            return false;
        }

        while (tokenIndex < Tokens.Length && Tokens[tokenIndex].Kind == GlobTokenKind.Star)
        {
            tokenIndex++;
        }

        return tokenIndex == Tokens.Length;
    }
}
