// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Internal;

internal static class GitConfigReader
{
    public static string? ResolveGlobalExcludePath(string gitDirectory)
    {
        foreach (var configPath in EnumerateCandidateConfigPaths(gitDirectory))
        {
            if (!File.Exists(configPath))
            {
                continue;
            }

            var value = TryReadCoreExcludesFile(configPath);
            if (value is null)
            {
                continue;
            }

            return ExpandAndResolvePath(value, Path.GetDirectoryName(configPath)!);
        }

        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdgConfigHome))
        {
            return Path.Combine(ExpandHome(xdgConfigHome), "git", "ignore");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(home) ? null : Path.Combine(home, ".config", "git", "ignore");
    }

    public static bool? ResolveIgnoreCase(string gitDirectory)
    {
        foreach (var configPath in EnumerateCandidateConfigPaths(gitDirectory))
        {
            if (!File.Exists(configPath))
            {
                continue;
            }

            var value = TryReadCoreValue(configPath, "ignorecase");
            if (value is null)
            {
                continue;
            }

            if (TryParseBoolean(value, out var ignoreCase))
            {
                return ignoreCase;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateCandidateConfigPaths(string gitDirectory)
    {
        yield return Path.Combine(gitDirectory, "config");

        var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdgConfigHome))
        {
            yield return Path.Combine(ExpandHome(xdgConfigHome), "git", "config");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
        {
            yield return Path.Combine(home, ".gitconfig");
            yield return Path.Combine(home, ".config", "git", "config");
        }
    }

    private static string? TryReadCoreExcludesFile(string configPath)
        => TryReadCoreValue(configPath, "excludesFile");

    private static string? TryReadCoreValue(string configPath, string keyName)
    {
        string? currentSection = null;
        foreach (var rawLine in File.ReadLines(configPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                currentSection = TryParseSectionName(line);
                continue;
            }

            if (!string.Equals(currentSection, "core", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            if (!string.Equals(key, keyName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return ParseValue(line[(separatorIndex + 1)..].Trim());
        }

        return null;
    }

    private static string? TryParseSectionName(string line)
    {
        var section = line[1..^1].Trim();
        if (section.Length == 0)
        {
            return null;
        }

        var quoteIndex = section.IndexOf('"');
        var separatorIndex = section.IndexOfAny(' ', '\t');
        var endIndex = quoteIndex >= 0 && separatorIndex >= 0
            ? Math.Min(quoteIndex, separatorIndex)
            : Math.Max(quoteIndex, separatorIndex);

        return endIndex > 0 ? section[..endIndex] : section;
    }

    private static string ParseValue(string value)
    {
        if (value.Length < 2 || value[0] != '"' || value[^1] != '"')
        {
            return value;
        }

        var content = value[1..^1];
        if (content.IndexOf('\\') < 0)
        {
            return content;
        }

        var buffer = new char[content.Length];
        var written = 0;
        for (var index = 0; index < content.Length; index++)
        {
            var current = content[index];
            if (current == '\\' && index + 1 < content.Length)
            {
                buffer[written++] = content[++index];
                continue;
            }

            buffer[written++] = current;
        }

        return new string(buffer, 0, written);
    }

    private static string ExpandAndResolvePath(string path, string baseDirectory)
    {
        var expanded = ExpandHome(path);
        return Path.IsPathRooted(expanded)
            ? Path.GetFullPath(expanded)
            : Path.GetFullPath(Path.Combine(baseDirectory, expanded));
    }

    private static string ExpandHome(string path)
    {
        if (!path.StartsWith("~/", StringComparison.Ordinal))
        {
            return path;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(home) ? path : Path.Combine(home, path[2..].Replace('/', Path.DirectorySeparatorChar));
    }

    private static bool TryParseBoolean(string value, out bool result)
    {
        switch (value.Trim().ToLowerInvariant())
        {
            case "true":
            case "yes":
            case "on":
            case "1":
                result = true;
                return true;
            case "false":
            case "no":
            case "off":
            case "0":
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }
}
