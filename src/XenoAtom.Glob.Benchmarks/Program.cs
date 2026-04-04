using BenchmarkDotNet.Running;

using XenoAtom.Glob.Benchmarks;

var mode = BenchmarkRunMode.Release;
var filteredArgs = new List<string>(args.Length);
foreach (var arg in args)
{
    if (string.Equals(arg, "--smoke", StringComparison.OrdinalIgnoreCase))
    {
        mode = BenchmarkRunMode.Smoke;
        continue;
    }

    if (string.Equals(arg, "--release", StringComparison.OrdinalIgnoreCase))
    {
        mode = BenchmarkRunMode.Release;
        continue;
    }

    filteredArgs.Add(arg);
}

BenchmarkSwitcher
    .FromAssembly(typeof(Program).Assembly)
    .Run(filteredArgs.ToArray(), BenchmarkConfigs.Create(mode));
