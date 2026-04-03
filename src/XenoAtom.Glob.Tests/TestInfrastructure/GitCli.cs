// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Diagnostics;
using System.Text;

namespace XenoAtom.Glob.Tests.TestInfrastructure;

internal sealed class GitCli
{
    private static readonly Lazy<string> LazyVersion = new(GetVersionCore);

    private GitCli(string workingDirectory)
    {
        WorkingDirectory = workingDirectory;
    }

    public string WorkingDirectory { get; }

    public static string Version => LazyVersion.Value;

    public static GitCli In(string workingDirectory) => new(workingDirectory);

    public GitCommandResult Run(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Unable to start git process.");
        }

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new GitCommandResult(
            process.ExitCode,
            standardOutput.Replace("\r\n", "\n", StringComparison.Ordinal),
            standardError.Replace("\r\n", "\n", StringComparison.Ordinal),
            string.Join(" ", startInfo.ArgumentList.Select(EscapeArgument)));
    }

    public GitCommandResult RunChecked(params string[] arguments)
    {
        var result = Run(arguments);
        if (result.ExitCode == 0)
        {
            return result;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"Git command failed: git {result.CommandLine}");
        builder.AppendLine($"Working directory: {WorkingDirectory}");
        builder.AppendLine($"Git version: {Version}");
        builder.AppendLine($"Exit code: {result.ExitCode}");
        if (!string.IsNullOrEmpty(result.StandardOutput))
        {
            builder.AppendLine("Standard output:");
            builder.Append(result.StandardOutput);
        }

        if (!string.IsNullOrEmpty(result.StandardError))
        {
            builder.AppendLine("Standard error:");
            builder.Append(result.StandardError);
        }

        throw new InvalidOperationException(builder.ToString());
    }

    private static string GetVersionCore()
    {
        var startInfo = new ProcessStartInfo("git", "--version")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            throw new InvalidOperationException("Unable to start git process.");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Unable to determine git version. Exit code: {process.ExitCode}. {error}");
        }

        return output.Trim();
    }

    private static string EscapeArgument(string value)
    {
        if (value.Length == 0 || value.Contains(' ') || value.Contains('"'))
        {
            return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
        }

        return value;
    }
}
