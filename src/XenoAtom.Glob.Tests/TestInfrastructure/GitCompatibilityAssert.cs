// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text;

using XenoAtom.Glob.Ignore;

namespace XenoAtom.Glob.Tests.TestInfrastructure;

internal static class GitCompatibilityAssert
{
    public static void Matches(
        string path,
        bool isDirectory,
        GitCli git,
        GitCheckIgnoreResult gitResult,
        IgnoreMatcher matcher,
        string? fixtureName = null)
    {
        var localResult = matcher.Evaluate(path, isDirectory);
        if (gitResult.IsIgnored == localResult.IsIgnored)
        {
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Git compatibility mismatch for path '{path}'.");
        builder.AppendLine($"Fixture: {fixtureName ?? "<none>"}");
        builder.AppendLine($"Git version: {GitCli.Version}");
        builder.AppendLine($"Working directory: {git.WorkingDirectory}");
        builder.AppendLine($"Is directory: {isDirectory}");
        builder.AppendLine($"Git ignored: {gitResult.IsIgnored}");
        builder.AppendLine($"Git source: {gitResult.Source ?? "<none>"}");
        builder.AppendLine($"Git line: {gitResult.LineNumber ?? "<none>"}");
        builder.AppendLine($"Git pattern: {gitResult.Pattern ?? "<none>"}");
        builder.AppendLine($"Local ignored: {localResult.IsIgnored}");
        builder.AppendLine($"Local matched: {localResult.IsMatch}");
        builder.AppendLine($"Local source: {localResult.Rule?.SourcePath ?? "<none>"}");
        builder.AppendLine($"Local line: {localResult.Rule?.LineNumber.ToString() ?? "<none>"}");
        builder.AppendLine($"Local pattern: {localResult.Rule?.RawPatternText ?? "<none>"}");
        builder.AppendLine("Git command: git check-ignore --no-index --stdin -z -v --non-matching");
        builder.Append(IgnoreTraceFormatter.Format(path, isDirectory, matcher));
        Assert.Fail(builder.ToString());
    }
}
