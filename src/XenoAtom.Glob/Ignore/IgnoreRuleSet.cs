// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.Ignore;

/// <summary>
/// Represents an immutable ordered set of ignore rules.
/// </summary>
public sealed class IgnoreRuleSet
{
    private IgnoreRuleSet(IReadOnlyList<IgnoreRule> rules)
    {
        Rules = rules;
    }

    /// <summary>
    /// Gets the parsed rules in source order.
    /// </summary>
    public IReadOnlyList<IgnoreRule> Rules { get; }

    /// <summary>
    /// Parses a Git-compatible ignore file from a character span.
    /// </summary>
    public static IgnoreRuleSet ParseGitIgnore(
        ReadOnlySpan<char> content,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory)
    {
        return Parse(content.ToString(), IgnoreDialect.GitIgnore, baseDirectory, sourcePath, sourceKind);
    }

    /// <summary>
    /// Parses a Git-compatible ignore file.
    /// </summary>
    public static IgnoreRuleSet ParseGitIgnore(
        string content,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory)
    {
        return Parse(content, IgnoreDialect.GitIgnore, baseDirectory, sourcePath, sourceKind);
    }

    /// <summary>
    /// Parses a Git-compatible ignore file from a text reader.
    /// </summary>
    public static IgnoreRuleSet ParseGitIgnore(
        TextReader reader,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory)
    {
        return Parse(reader, IgnoreDialect.GitIgnore, baseDirectory, sourcePath, sourceKind);
    }

    /// <summary>
    /// Parses a Git-compatible ignore file from a stream.
    /// </summary>
    public static IgnoreRuleSet ParseGitIgnore(
        Stream stream,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory,
        Encoding? encoding = null)
    {
        return Parse(stream, IgnoreDialect.GitIgnore, baseDirectory, sourcePath, sourceKind, encoding);
    }

    /// <summary>
    /// Parses an ignore file using the specified dialect from a character span.
    /// </summary>
    public static IgnoreRuleSet Parse(
        ReadOnlySpan<char> content,
        IgnoreDialect dialect,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory)
    {
        return Parse(content.ToString(), dialect, baseDirectory, sourcePath, sourceKind);
    }

    /// <summary>
    /// Parses an ignore file using the specified dialect.
    /// </summary>
    public static IgnoreRuleSet Parse(
        string content,
        IgnoreDialect dialect,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory)
    {
        ArgumentNullException.ThrowIfNull(content);

        var normalizedBaseDirectory = string.IsNullOrEmpty(baseDirectory)
            ? string.Empty
            : PathNormalizer.NormalizeRelativePath(baseDirectory!, isDirectory: true).Value;

        var rules = dialect switch
        {
            IgnoreDialect.GitIgnore => GitIgnoreParser.Parse(content, normalizedBaseDirectory, sourcePath, sourceKind),
            IgnoreDialect.IgnoreFile => GitIgnoreParser.Parse(content, normalizedBaseDirectory, sourcePath, sourceKind),
            _ => throw new ArgumentOutOfRangeException(nameof(dialect)),
        };

        return new IgnoreRuleSet(rules);
    }

    /// <summary>
    /// Parses an ignore file using the specified dialect from a text reader.
    /// </summary>
    public static IgnoreRuleSet Parse(
        TextReader reader,
        IgnoreDialect dialect,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return Parse(reader.ReadToEnd(), dialect, baseDirectory, sourcePath, sourceKind);
    }

    /// <summary>
    /// Parses an ignore file using the specified dialect from a stream.
    /// </summary>
    public static IgnoreRuleSet Parse(
        Stream stream,
        IgnoreDialect dialect,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory,
        Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return Parse(reader.ReadToEnd(), dialect, baseDirectory, sourcePath, sourceKind);
    }

    /// <summary>
    /// Parses an <c>.ignore</c> file using the current dialect implementation.
    /// </summary>
    public static IgnoreRuleSet ParseIgnoreFile(
        string content,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory)
    {
        return Parse(content, IgnoreDialect.IgnoreFile, baseDirectory, sourcePath, sourceKind);
    }
}
