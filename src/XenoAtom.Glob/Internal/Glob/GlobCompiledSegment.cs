// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace XenoAtom.Glob.Internal;

internal sealed class GlobCompiledSegment
{
    private readonly bool _isLiteral;
    private readonly string? _literalText;

    public GlobCompiledSegment(string rawText, GlobToken[] tokens, bool isRecursiveWildcard)
    {
        RawText = rawText;
        Tokens = tokens;
        IsRecursiveWildcard = isRecursiveWildcard;

        var totalLiteralLength = 0;
        _isLiteral = !isRecursiveWildcard;
        if (_isLiteral)
        {
            for (var index = 0; index < tokens.Length; index++)
            {
                var token = tokens[index];
                if (token.Kind != GlobTokenKind.Literal)
                {
                    _isLiteral = false;
                    break;
                }

                totalLiteralLength += token.Literal!.Length;
            }
        }

        if (_isLiteral)
        {
            _literalText = tokens.Length switch
            {
                0 => string.Empty,
                1 => tokens[0].Literal,
                _ => CreateLiteralText(tokens, totalLiteralLength),
            };
        }
    }

    public string RawText { get; }

    public GlobToken[] Tokens { get; }

    public bool IsRecursiveWildcard { get; }

    public bool IsLiteral => _isLiteral;

    public string? LiteralText => _literalText;

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
                        if (token.CharClass!.IsMatch(value[valueIndex], comparison))
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

                        if (starTokenIndex >= 0 && starTokenIndex == tokenIndex - 1)
                        {
                            var literalIndex = value[valueIndex..].IndexOf(literal, comparison.Value);
                            if (literalIndex < 0)
                            {
                                return false;
                            }

                            starValueIndex = valueIndex + literalIndex;
                            tokenIndex++;
                            valueIndex = starValueIndex + literal.Length;
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

    private static string CreateLiteralText(GlobToken[] tokens, int totalLength)
    {
        var builder = new StringBuilder(totalLength);
        for (var index = 0; index < tokens.Length; index++)
        {
            builder.Append(tokens[index].Literal);
        }

        return builder.ToString();
    }
}
