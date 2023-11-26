﻿namespace Tests;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using Xunit;

public class BenchmarkTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public void BenchmarkDotNet_Perf()
    {
        var logger = new AccumulationLogger();
        BenchmarkRunner.Run<FloatingBenchmarks>(DefaultConfig.Instance.AddLogger(logger));
        output.WriteLine(logger.GetLog());
    }
}

public class FloatingBenchmarks
{
    decimal m1;
    double d1;
    decimal m2;
    double d2;

    [GlobalSetup]
    public void Setup()
    {
        d1 = 12345.6789;
        d2 = 1234.56778;
        m1 = 12345.6789M;
        m2 = 1234.56778M;
    }

    [Benchmark(Baseline = true)]
    public double DoubleAdd() => d1 + d2;

    [Benchmark]
    public decimal DecimalAdd() => m1 + m2;
}