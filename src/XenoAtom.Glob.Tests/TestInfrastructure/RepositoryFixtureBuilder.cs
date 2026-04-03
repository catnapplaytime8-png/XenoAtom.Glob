// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

namespace XenoAtom.Glob.Tests.TestInfrastructure;

internal sealed class RepositoryFixtureBuilder : IDisposable
{
    private readonly TemporaryDirectory _root = new();

    public TemporaryDirectory Root => _root;

    public RepositoryFixtureBuilder CreateTypicalRepository()
    {
        _root.WriteAllText(".gitignore", """
            bin/
            obj/
            *.tmp
            .cache/
            """);
        _root.WriteAllText("src/.gitignore", """
            generated/*
            !generated/include.txt
            """);
        _root.WriteAllText("README.md", string.Empty);
        _root.WriteAllText("src/app/app.cs", string.Empty);
        _root.WriteAllText("src/lib/util.cs", string.Empty);
        _root.WriteAllText("src/generated/file.g.cs", string.Empty);
        _root.WriteAllText("src/generated/include.txt", string.Empty);
        _root.WriteAllText("bin/output.dll", string.Empty);
        _root.WriteAllText("obj/output.obj", string.Empty);
        _root.WriteAllText(".cache/temp.tmp", string.Empty);
        return this;
    }

    public RepositoryFixtureBuilder CreateDeepTree(int depth, int filesPerDirectory)
    {
        var current = "deep";
        for (var i = 0; i < depth; i++)
        {
            for (var fileIndex = 0; fileIndex < filesPerDirectory; fileIndex++)
            {
                _root.WriteAllText($"{current}/file_{i}_{fileIndex}.txt", string.Empty);
            }

            current = $"{current}/level{i}";
        }

        return this;
    }

    public RepositoryFixtureBuilder CreateWideTree(int directoryCount, int filesPerDirectory)
    {
        for (var dir = 0; dir < directoryCount; dir++)
        {
            for (var file = 0; file < filesPerDirectory; file++)
            {
                _root.WriteAllText($"wide/dir{dir}/file{file}.txt", string.Empty);
            }
        }

        return this;
    }

    public RepositoryFixtureBuilder CreateManySmallIgnoreFiles(int depth, int filesPerDirectory)
    {
        var current = "stack";
        for (var i = 0; i < depth; i++)
        {
            _root.WriteAllText($"{current}/.gitignore", $"skip{i}.tmp\nignored{i}/\n");
            _root.WriteAllText($"{current}/skip{i}.tmp", string.Empty);
            for (var file = 0; file < filesPerDirectory; file++)
            {
                _root.WriteAllText($"{current}/keep{i}_{file}.txt", string.Empty);
                _root.WriteAllText($"{current}/ignored{i}/drop{file}.txt", string.Empty);
            }

            current = $"{current}/level{i}";
        }

        return this;
    }

    public void Dispose()
    {
        _root.Dispose();
    }
}
