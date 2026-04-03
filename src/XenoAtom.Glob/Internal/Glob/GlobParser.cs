// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

namespace XenoAtom.Glob.Internal;

internal static class GlobParser
{
    public static GlobParseResult TryParse(string pattern, GlobParserOptions options)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        if (pattern.Length == 0)
        {
            return GlobParseResult.FromPattern(new GlobCompiledPattern([], false, false, GlobPatternKind.Empty, string.Empty, null, null));
        }

        var hasLeadingSeparator = pattern[0] == '/';
        if (hasLeadingSeparator && !options.AllowLeadingSeparator)
        {
            return GlobParseResult.Failure(GlobPatternParseError.LeadingSeparatorNotAllowed);
        }

        var segments = new List<GlobCompiledSegment>();
        var segmentBuilder = new StringBuilder();
        var index = 0;
        var inCharClass = false;
        var escaped = false;

        while (index < pattern.Length)
        {
            var c = pattern[index];
            if (escaped)
            {
                segmentBuilder.Append('\\');
                segmentBuilder.Append(c);
                escaped = false;
                index++;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                index++;
                continue;
            }

            if (c == '[')
            {
                inCharClass = true;
                segmentBuilder.Append(c);
                index++;
                continue;
            }

            if (c == ']' && inCharClass)
            {
                inCharClass = false;
                segmentBuilder.Append(c);
                index++;
                continue;
            }

            if (c == '/' && !inCharClass)
            {
                if (segmentBuilder.Length > 0)
                {
                    var parsedSegment = ParseSegment(segmentBuilder.ToString());
                    if (!parsedSegment.Success)
                    {
                        return parsedSegment;
                    }

                    segments.Add(parsedSegment.Pattern.Segments[0]);
                    segmentBuilder.Clear();
                }

                index++;
                continue;
            }

            segmentBuilder.Append(c);
            index++;
        }

        if (escaped)
        {
            return GlobParseResult.Failure(GlobPatternParseError.InvalidEscapeSequence);
        }

        if (inCharClass)
        {
            return GlobParseResult.Failure(GlobPatternParseError.UnterminatedCharacterClass);
        }

        var hasTrailingSeparator = pattern.Length > 0 && pattern[^1] == '/';
        if (hasTrailingSeparator && !options.AllowTrailingSeparator)
        {
            return GlobParseResult.Failure(GlobPatternParseError.TrailingSeparatorNotAllowed);
        }

        if (segmentBuilder.Length > 0)
        {
            var parsedSegment = ParseSegment(segmentBuilder.ToString());
            if (!parsedSegment.Success)
            {
                return parsedSegment;
            }

            segments.Add(parsedSegment.Pattern.Segments[0]);
        }

        var compiledPattern = CreateCompiledPattern(segments.ToArray(), hasLeadingSeparator, hasTrailingSeparator);
        return GlobParseResult.FromPattern(compiledPattern);
    }

    private static GlobParseResult ParseSegment(string segmentText)
    {
        if (segmentText == "**")
        {
            return GlobParseResult.FromPattern(new GlobCompiledPattern(
                [new GlobCompiledSegment(segmentText, [], true)],
                false,
                false,
                GlobPatternKind.RecursiveMatchAll,
                null,
                null,
                null));
        }

        var tokens = new List<GlobToken>();
        var literalBuilder = new StringBuilder();
        for (var index = 0; index < segmentText.Length; index++)
        {
            var c = segmentText[index];
            switch (c)
            {
                case '\\':
                    if (index == segmentText.Length - 1)
                    {
                        return GlobParseResult.Failure(GlobPatternParseError.InvalidEscapeSequence);
                    }

                    literalBuilder.Append(segmentText[++index]);
                    break;

                case '*':
                    FlushLiteral(tokens, literalBuilder);
                    tokens.Add(GlobToken.StarToken);
                    break;

                case '?':
                    FlushLiteral(tokens, literalBuilder);
                    tokens.Add(GlobToken.QuestionToken);
                    break;

                case '[':
                    FlushLiteral(tokens, literalBuilder);
                    var parsedCharClass = TryParseCharClass(segmentText, index);
                    if (!parsedCharClass.Success)
                    {
                        return GlobParseResult.Failure(parsedCharClass.Error);
                    }

                    tokens.Add(GlobToken.CharClassToken(parsedCharClass.CharClass));
                    index = parsedCharClass.NextIndex;
                    break;

                default:
                    literalBuilder.Append(c);
                    break;
            }
        }

        FlushLiteral(tokens, literalBuilder);
        return GlobParseResult.FromPattern(new GlobCompiledPattern(
            [new GlobCompiledSegment(segmentText, tokens.ToArray(), false)],
            false,
            false,
            GlobPatternKind.General,
            null,
            null,
            null));
    }

    private static GlobCompiledPattern CreateCompiledPattern(
        GlobCompiledSegment[] segments,
        bool hasLeadingSeparator,
        bool hasTrailingSeparator)
    {
        var kind = GlobPatternKind.General;
        string? exactText = null;
        string? prefixText = null;
        string? suffixText = null;

        if (segments.Length == 1)
        {
            var segment = segments[0];
            if (segment.IsRecursiveWildcard)
            {
                kind = GlobPatternKind.RecursiveMatchAll;
            }
            else if (segment.Tokens.Length == 1 && segment.Tokens[0].Kind == GlobTokenKind.Star)
            {
                kind = GlobPatternKind.MatchAll;
            }
            else if (segment.IsLiteral)
            {
                kind = GlobPatternKind.Exact;
                exactText = segment.LiteralText;
            }
            else if (segment.Tokens.Length == 2 &&
                     segment.Tokens[0].Kind == GlobTokenKind.Star &&
                     segment.Tokens[1].Kind == GlobTokenKind.Literal)
            {
                kind = GlobPatternKind.Suffix;
                suffixText = segment.Tokens[1].Literal;
            }
            else if (segment.Tokens.Length == 2 &&
                     segment.Tokens[0].Kind == GlobTokenKind.Literal &&
                     segment.Tokens[1].Kind == GlobTokenKind.Star)
            {
                kind = GlobPatternKind.Prefix;
                prefixText = segment.Tokens[0].Literal;
            }
        }
        else if (segments.Length > 1 && segments.All(static x => x.IsLiteral))
        {
            kind = GlobPatternKind.Exact;
            exactText = string.Join("/", segments.Select(static x => x.LiteralText));
        }

        return new GlobCompiledPattern(segments, hasLeadingSeparator, hasTrailingSeparator, kind, exactText, prefixText, suffixText);
    }

    private static void FlushLiteral(List<GlobToken> tokens, StringBuilder literalBuilder)
    {
        if (literalBuilder.Length == 0)
        {
            return;
        }

        tokens.Add(GlobToken.LiteralToken(literalBuilder.ToString()));
        literalBuilder.Clear();
    }

    private static ParsedCharClass TryParseCharClass(string text, int openingBracketIndex)
    {
        var index = openingBracketIndex + 1;
        var isNegated = false;
        if (index < text.Length && text[index] == '!')
        {
            isNegated = true;
            index++;
        }

        var ranges = new List<GlobCharClassRange>();
        var hasAny = false;
        while (index < text.Length)
        {
            if (text[index] == ']' && hasAny)
            {
                return ParsedCharClass.FromCharClass(new GlobCharClass(isNegated, ranges.ToArray()), index);
            }

            char start;
            if (text[index] == '\\')
            {
                index++;
                if (index >= text.Length)
                {
                    return ParsedCharClass.Failure(GlobPatternParseError.InvalidEscapeSequence);
                }

                start = text[index++];
            }
            else
            {
                start = text[index++];
            }

            if (index + 1 < text.Length && text[index] == '-' && text[index + 1] != ']')
            {
                index++;
                char end;
                if (text[index] == '\\')
                {
                    index++;
                    if (index >= text.Length)
                    {
                        return ParsedCharClass.Failure(GlobPatternParseError.InvalidEscapeSequence);
                    }

                    end = text[index++];
                }
                else
                {
                    end = text[index++];
                }

                if (end < start)
                {
                    return ParsedCharClass.Failure(GlobPatternParseError.InvalidCharacterClassRange);
                }

                ranges.Add(new GlobCharClassRange(start, end));
            }
            else
            {
                ranges.Add(new GlobCharClassRange(start, start));
            }

            hasAny = true;
        }

        return ParsedCharClass.Failure(GlobPatternParseError.UnterminatedCharacterClass);
    }

    private readonly record struct ParsedCharClass(bool Success, GlobCharClass CharClass, int NextIndex, GlobPatternParseError Error)
    {
        public static ParsedCharClass FromCharClass(GlobCharClass charClass, int nextIndex) => new(true, charClass, nextIndex, GlobPatternParseError.None);

        public static ParsedCharClass Failure(GlobPatternParseError error) => new(false, null!, -1, error);
    }
}
