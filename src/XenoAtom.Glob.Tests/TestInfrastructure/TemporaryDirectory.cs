// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Tests.TestInfrastructure;

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"xenoatom_glob_{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string GetPath(params string[] parts)
    {
        if (parts.Length == 0)
        {
            return Path;
        }

        var allParts = new string[parts.Length + 1];
        allParts[0] = Path;
        Array.Copy(parts, 0, allParts, 1, parts.Length);
        return System.IO.Path.Combine(allParts);
    }

    public void CreateDirectory(params string[] parts) => Directory.CreateDirectory(GetPath(parts));

    public void WriteAllText(string relativePath, string content)
    {
        var fullPath = GetPath(relativePath);
        var directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
