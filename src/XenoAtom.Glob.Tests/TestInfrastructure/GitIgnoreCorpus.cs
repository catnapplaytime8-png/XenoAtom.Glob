// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Text.Json;

namespace XenoAtom.Glob.Tests.TestInfrastructure;

internal sealed class GitIgnoreCorpus
{
    public static IReadOnlyList<GitIgnoreCorpusFixture> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "git_ignore_corpus.json");
        var json = File.ReadAllText(path);
        var fixtures = JsonSerializer.Deserialize<List<GitIgnoreCorpusFixture>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        return fixtures ?? throw new InvalidOperationException("Unable to load git ignore corpus.");
    }
}

internal sealed class GitIgnoreCorpusFixture
{
    public string Name { get; set; } = string.Empty;

    public Dictionary<string, string> Files { get; set; } = new(StringComparer.Ordinal);

    public List<string> Paths { get; set; } = [];
}
