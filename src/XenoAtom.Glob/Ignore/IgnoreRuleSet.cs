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
    /// <param name="content">The ignore file content.</param>
    /// <param name="baseDirectory">The directory containing the ignore file, relative to the evaluation root.</param>
    /// <param name="sourcePath">The optional source path used for diagnostics.</param>
    /// <param name="sourceKind">The rule source kind.</param>
    /// <returns>A parsed ignore rule set.</returns>
    public static IgnoreRuleSet ParseGitIgnore(
        ReadOnlySpan<char> content,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory)
    {
        return ParseGitIgnore(content.ToString(), baseDirectory, sourcePath, sourceKind);
    }

    /// <summary>
    /// Parses a Git-compatible ignore file.
    /// </summary>
    /// <param name="content">The ignore file content.</param>
    /// <param name="baseDirectory">The directory containing the ignore file, relative to the evaluation root.</param>
    /// <param name="sourcePath">The optional source path used for diagnostics.</param>
    /// <param name="sourceKind">The rule source kind.</param>
    /// <returns>A parsed ignore rule set.</returns>
    public static IgnoreRuleSet ParseGitIgnore(
        string content,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory)
    {
        ArgumentNullException.ThrowIfNull(content);

        var normalizedBaseDirectory = string.IsNullOrEmpty(baseDirectory)
            ? string.Empty
            : PathNormalizer.NormalizeRelativePath(baseDirectory!, isDirectory: true).Value;

        var rules = GitIgnoreParser.Parse(content, normalizedBaseDirectory, sourcePath, sourceKind);
        return new IgnoreRuleSet(rules);
    }

    /// <summary>
    /// Parses a Git-compatible ignore file from a text reader.
    /// </summary>
    /// <param name="reader">The text reader supplying the ignore file content.</param>
    /// <param name="baseDirectory">The directory containing the ignore file, relative to the evaluation root.</param>
    /// <param name="sourcePath">The optional source path used for diagnostics.</param>
    /// <param name="sourceKind">The rule source kind.</param>
    /// <returns>A parsed ignore rule set.</returns>
    public static IgnoreRuleSet ParseGitIgnore(
        TextReader reader,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ParseGitIgnore(reader.ReadToEnd(), baseDirectory, sourcePath, sourceKind);
    }

    /// <summary>
    /// Parses a Git-compatible ignore file from a stream.
    /// </summary>
    /// <param name="stream">The stream containing the ignore file content.</param>
    /// <param name="baseDirectory">The directory containing the ignore file, relative to the evaluation root.</param>
    /// <param name="sourcePath">The optional source path used for diagnostics.</param>
    /// <param name="sourceKind">The rule source kind.</param>
    /// <param name="encoding">The optional text encoding. UTF-8 is used by default.</param>
    /// <returns>A parsed ignore rule set.</returns>
    public static IgnoreRuleSet ParseGitIgnore(
        Stream stream,
        string? baseDirectory = null,
        string? sourcePath = null,
        IgnoreRuleSourceKind sourceKind = IgnoreRuleSourceKind.PerDirectory,
        Encoding? encoding = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = new StreamReader(stream, encoding ?? Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return ParseGitIgnore(reader.ReadToEnd(), baseDirectory, sourcePath, sourceKind);
    }
}
