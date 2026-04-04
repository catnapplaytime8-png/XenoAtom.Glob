// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Internal;

internal static class GitIgnoreParser
{
    public static IReadOnlyList<IgnoreRule> Parse(
        string content,
        string baseDirectory,
        string? sourcePath,
        IgnoreRuleSourceKind sourceKind)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Parse(content.AsSpan(), baseDirectory, sourcePath, sourceKind);
    }

    public static IReadOnlyList<IgnoreRule> Parse(
        ReadOnlySpan<char> content,
        string baseDirectory,
        string? sourcePath,
        IgnoreRuleSourceKind sourceKind)
    {
        var rules = new List<IgnoreRule>();
        var lineNumber = 0;
        var lineStart = 0;
        for (var index = 0; index <= content.Length; index++)
        {
            if (index < content.Length && content[index] != '\n')
            {
                continue;
            }

            var line = content[lineStart..index];
            if (line.Length > 0 && line[^1] == '\r')
            {
                line = line[..^1];
            }

            lineNumber++;
            var rule = ParseLine(line, baseDirectory, sourcePath, sourceKind, lineNumber);
            if (rule is not null)
            {
                rules.Add(rule);
            }

            lineStart = index + 1;
        }

        return rules;
    }

    private static IgnoreRule? ParseLine(
        ReadOnlySpan<char> line,
        string baseDirectory,
        string? sourcePath,
        IgnoreRuleSourceKind sourceKind,
        int lineNumber)
    {
        if (line.Length == 0)
        {
            return null;
        }

        var trimmedLine = TrimUnescapedTrailingSpaces(line);
        if (trimmedLine.Length == 0)
        {
            return null;
        }

        if (trimmedLine[0] == '#')
        {
            return null;
        }

        var isNegated = false;
        var patternText = trimmedLine;
        if (patternText[0] == '!')
        {
            isNegated = true;
            patternText = patternText[1..];
        }

        if (patternText.Length == 0)
        {
            return null;
        }

        var directoryOnly = HasUnescapedTrailingSlash(patternText);
        if (directoryOnly)
        {
            patternText = patternText[..^1];
        }

        var leadingSlash = patternText.Length > 0 && patternText[0] == '/';
        if (leadingSlash)
        {
            patternText = patternText[1..];
        }

        var basenameOnly = !ContainsUnescapedSlash(patternText);
        var parseResult = GlobParser.TryParse(patternText.ToString(), GlobParserOptions.IgnorePattern);
        if (!parseResult.Success)
        {
            throw new ArgumentException(
                $"Invalid ignore rule at line {lineNumber}: {parseResult.Error}",
                nameof(line));
        }

        return new IgnoreRule(
            patternText.ToString(),
            trimmedLine.ToString(),
            isNegated,
            directoryOnly,
            basenameOnly,
            baseDirectory,
            lineNumber,
            sourcePath,
            sourceKind,
            parseResult.Pattern);
    }

    private static ReadOnlySpan<char> TrimUnescapedTrailingSpaces(ReadOnlySpan<char> line)
    {
        var end = line.Length;
        while (end > 0 && line[end - 1] == ' ' && !IsEscaped(line, end - 1))
        {
            end--;
        }

        return line[..end];
    }

    private static bool HasUnescapedTrailingSlash(ReadOnlySpan<char> text) => text.Length > 0 && text[^1] == '/' && !IsEscaped(text, text.Length - 1);

    private static bool ContainsUnescapedSlash(ReadOnlySpan<char> text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '/' && !IsEscaped(text, index))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEscaped(ReadOnlySpan<char> text, int index)
    {
        var slashCount = 0;
        for (var i = index - 1; i >= 0 && text[i] == '\\'; i--)
        {
            slashCount++;
        }

        return (slashCount & 1) == 1;
    }
}
