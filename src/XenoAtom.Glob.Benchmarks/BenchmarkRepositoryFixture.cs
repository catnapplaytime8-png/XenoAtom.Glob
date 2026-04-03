using XenoAtom.Glob.Git;

namespace XenoAtom.Glob.Benchmarks;

internal sealed class BenchmarkRepositoryFixture : IDisposable
{
    public BenchmarkRepositoryFixture()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"xenoatom_glob_bench_{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
    }

    public string RootPath { get; }

    public RepositoryContext InitializeGitRepository()
    {
        var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = RootPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            ArgumentList = { "init", "--quiet" },
        });

        process!.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(process.StandardError.ReadToEnd());
        }

        return RepositoryDiscovery.Discover(RootPath);
    }

    public void WriteAllText(string relativePath, string content)
    {
        var fullPath = Path.Combine(RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content);
    }

    public void CreateDeepTree(int depth, int filesPerDirectory)
    {
        var current = "deep";
        for (var i = 0; i < depth; i++)
        {
            for (var j = 0; j < filesPerDirectory; j++)
            {
                WriteAllText($"{current}/file_{i}_{j}.txt", string.Empty);
            }

            current = $"{current}/level{i}";
        }
    }

    public void CreateWideTree(int directoryCount, int filesPerDirectory)
    {
        for (var i = 0; i < directoryCount; i++)
        {
            for (var j = 0; j < filesPerDirectory; j++)
            {
                WriteAllText($"wide/dir{i}/file{j}.txt", string.Empty);
            }
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, true);
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
