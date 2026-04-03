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
        var rules = new List<IgnoreRule>();
        using var reader = new StringReader(content);
        string? line;
        var lineNumber = 0;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var rule = ParseLine(line, baseDirectory, sourcePath, sourceKind, lineNumber);
            if (rule is not null)
            {
                rules.Add(rule);
            }
        }

        return rules;
    }

    private static IgnoreRule? ParseLine(
        string line,
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
        var parseResult = GlobParser.TryParse(patternText, GlobParserOptions.IgnorePattern);
        if (!parseResult.Success)
        {
            throw new ArgumentException(
                $"Invalid ignore rule at line {lineNumber}: {parseResult.Error}",
                nameof(line));
        }

        return new IgnoreRule(
            patternText,
            trimmedLine,
            isNegated,
            directoryOnly,
            basenameOnly,
            baseDirectory,
            lineNumber,
            sourcePath,
            sourceKind,
            parseResult.Pattern);
    }

    private static string TrimUnescapedTrailingSpaces(string line)
    {
        var end = line.Length;
        while (end > 0 && line[end - 1] == ' ' && !IsEscaped(line, end - 1))
        {
            end--;
        }

        return line[..end];
    }

    private static bool HasUnescapedTrailingSlash(string text) => text.Length > 0 && text[^1] == '/' && !IsEscaped(text, text.Length - 1);

    private static bool ContainsUnescapedSlash(string text)
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

    private static bool IsEscaped(string text, int index)
    {
        var slashCount = 0;
        for (var i = index - 1; i >= 0 && text[i] == '\\'; i--)
        {
            slashCount++;
        }

        return (slashCount & 1) == 1;
    }
}
