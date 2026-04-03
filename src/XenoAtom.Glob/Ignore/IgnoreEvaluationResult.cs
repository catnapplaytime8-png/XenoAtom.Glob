// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Ignore;

/// <summary>
/// Represents the result of evaluating ignore rules against a path.
/// </summary>
public readonly record struct IgnoreEvaluationResult
{
    internal IgnoreEvaluationResult(bool isMatch, bool isIgnored, IgnoreRule? rule)
    {
        IsMatch = isMatch;
        IsIgnored = isIgnored;
        Rule = rule;
    }

    /// <summary>
    /// Gets a value indicating whether any rule matched.
    /// </summary>
    public bool IsMatch { get; }

    /// <summary>
    /// Gets a value indicating whether the final decision is to ignore the path.
    /// </summary>
    public bool IsIgnored { get; }

    /// <summary>
    /// Gets the winning rule when <see cref="IsMatch"/> is <see langword="true"/>.
    /// </summary>
    public IgnoreRule? Rule { get; }
}
