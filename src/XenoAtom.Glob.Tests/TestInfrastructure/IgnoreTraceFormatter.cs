// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

using XenoAtom.Glob.Ignore;
using XenoAtom.Glob.Internal;

namespace XenoAtom.Glob.Tests.TestInfrastructure;

internal static class IgnoreTraceFormatter
{
    public static string Format(string path, bool isDirectory, IgnoreMatcher matcher)
    {
        var normalizedPath = PathNormalizer.NormalizeRelativePath(path, isDirectory);
        var trace = matcher.EvaluateWithTrace(path, isDirectory);
        var builder = new StringBuilder();
        builder.AppendLine($"Normalized path: {normalizedPath.Value}");
        builder.AppendLine($"Is directory: {isDirectory}");
        builder.AppendLine($"Local matched: {trace.Result.IsMatch}");
        builder.AppendLine($"Local ignored: {trace.Result.IsIgnored}");
        builder.AppendLine($"Winning source: {trace.Result.Rule?.SourcePath ?? "<none>"}");
        builder.AppendLine($"Winning line: {trace.Result.Rule?.LineNumber.ToString() ?? "<none>"}");
        builder.AppendLine($"Winning pattern: {trace.Result.Rule?.RawPatternText ?? "<none>"}");
        builder.AppendLine("Matched rule stack:");

        if (trace.MatchedRules.Count == 0)
        {
            builder.AppendLine("  <none>");
            return builder.ToString();
        }

        foreach (var rule in trace.MatchedRules)
        {
            builder.Append("  ");
            builder.Append(rule.IsNegated ? "include " : "ignore ");
            builder.Append(rule.SourcePath ?? "<none>");
            builder.Append(':');
            builder.Append(rule.LineNumber);
            builder.Append(" -> ");
            builder.AppendLine(rule.RawPatternText);
        }

        return builder.ToString();
    }
}
