// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob;

/// <summary>
/// Represents a compiled glob pattern that can match relative paths.
/// </summary>
public sealed class GlobPattern
{
    private static readonly PathStringComparison DefaultComparison = PathStringComparison.Ordinal;
    private readonly GlobCompiledPattern _compiledPattern;

    internal GlobPattern(string pattern, GlobCompiledPattern compiledPattern)
    {
        Pattern = pattern;
        _compiledPattern = compiledPattern;
    }

    /// <summary>
    /// Gets the original pattern text.
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// Attempts to parse a glob pattern.
    /// </summary>
    /// <param name="pattern">The pattern to parse.</param>
    /// <returns>A parse result that contains either the compiled pattern or the parse error.</returns>
    public static GlobPatternParseResult TryParse(string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var result = GlobParser.TryParse(pattern, GlobParserOptions.Default);
        if (!result.Success)
        {
            return new GlobPatternParseResult(null, result.Error);
        }

        return new GlobPatternParseResult(new GlobPattern(pattern, result.Pattern), GlobPatternParseError.None);
    }

    /// <summary>
    /// Parses a glob pattern.
    /// </summary>
    /// <param name="pattern">The pattern to parse.</param>
    /// <returns>The compiled pattern.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pattern"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="pattern"/> is invalid.</exception>
    public static GlobPattern Parse(string pattern)
    {
        var result = TryParse(pattern);
        if (result.Success)
        {
            return result.Pattern!;
        }

        throw new ArgumentException(GetErrorMessage(result.Error), nameof(pattern));
    }

    /// <summary>
    /// Determines whether the pattern matches the specified relative path.
    /// </summary>
    /// <param name="path">The relative path to match.</param>
    /// <param name="isDirectory">A value indicating whether the path represents a directory.</param>
    /// <returns><see langword="true"/> if the pattern matches; otherwise <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is not a supported relative path.</exception>
    public bool IsMatch(string path, bool isDirectory = false)
    {
        var normalizedPath = PathNormalizer.NormalizeRelativePath(path, isDirectory);
        return _compiledPattern.Match(normalizedPath, DefaultComparison);
    }

    private static string GetErrorMessage(GlobPatternParseError error) => error switch
    {
        GlobPatternParseError.LeadingSeparatorNotAllowed => "The pattern cannot start with a path separator.",
        GlobPatternParseError.TrailingSeparatorNotAllowed => "The pattern cannot end with a path separator.",
        GlobPatternParseError.InvalidEscapeSequence => "The pattern contains an invalid trailing escape sequence.",
        GlobPatternParseError.UnterminatedCharacterClass => "The pattern contains an unterminated character class.",
        GlobPatternParseError.InvalidCharacterClassRange => "The pattern contains an invalid character class range.",
        _ => "The pattern could not be parsed.",
    };
}
