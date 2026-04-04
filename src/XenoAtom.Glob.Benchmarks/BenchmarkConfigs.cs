using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace XenoAtom.Glob.Benchmarks;

internal enum BenchmarkRunMode
{
    Release,
    Smoke,
}

internal static class BenchmarkConfigs
{
    public static IConfig Create(BenchmarkRunMode mode)
    {
        return mode switch
        {
            BenchmarkRunMode.Release => ManualConfig
                .Create(DefaultConfig.Instance)
                .AddJob(Job.Default.WithId("Release").WithWarmupCount(6).WithIterationCount(15)),
            BenchmarkRunMode.Smoke => ManualConfig
                .Create(DefaultConfig.Instance)
                .AddJob(Job.Default.WithId("Smoke").WithWarmupCount(1).WithIterationCount(3)),
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
    }
}
