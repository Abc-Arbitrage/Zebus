using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Abc.Zebus.Util;

namespace Abc.Zebus.Testing.Measurements
{
    public static class Measure
    {
        private const double _µsInOneSecond = 1000000;
        private static readonly object _lock = new object();
        private static readonly double _µsFrequency = _µsInOneSecond / Stopwatch.Frequency;

        public static void Execution(long iterations, Action action)
        {
            Execution(conf =>
            {
                conf.Action = i => action();
                conf.Iteration = (int)iterations;

            });
        }

        public static void Execution(Action<MeasureConfiguration> configurationAction)
        {
            var configuration = new MeasureConfiguration();
            configurationAction(configuration);

            if (configuration.Action == null)
                throw new ArgumentException("Action should be specified");

            Bench(configuration.Action, configuration.WarmUpIteration);

            var results = Bench(configuration.Action, configuration.Iteration);

            PrintResults(configuration, results);
        }

        private static BenchResults Bench(Action<long> action, long iterationCount)
        {
            GC.Collect();
            var ticks = new List<long>((int)iterationCount);
            var maxIteration = 0L;
            var maxTickCount = 0L;

            var stopwatch = new Stopwatch();
            var g0Count = GC.CollectionCount(0);
            var g1Count = GC.CollectionCount(1);
            var g2Count = GC.CollectionCount(2);
            stopwatch.Start();
            for (long i = 0; i < iterationCount; i++)
            {
                var originalTickCount = stopwatch.ElapsedTicks;
                action(i);
                var tickCount = stopwatch.ElapsedTicks - originalTickCount;
                ticks.Add(tickCount);
                if (tickCount <= maxTickCount)
                    continue;

                maxIteration = i;
                maxTickCount = tickCount;
            }
            stopwatch.Stop();
            g0Count = GC.CollectionCount(0) - g0Count;
            g1Count = GC.CollectionCount(1) - g1Count;
            g2Count = GC.CollectionCount(2) - g2Count;

            return new BenchResults
                       {
                           Elapsed = stopwatch.Elapsed,
                           G0Count = g0Count,
                           G1Count = g1Count,
                           G2Count = g2Count,
                           MaxIterationIndex = maxIteration,
                           Ticks = ticks
                       };
        }

        private static void PrintResults(MeasureConfiguration configuration, BenchResults results)
        {
            results.Ticks.Sort();
            var min = results.Ticks.First();
            var max = results.Ticks.Last();
            var onePercentIndex = (int)Math.Floor((decimal)results.Ticks.Count * 1 / 100) + 1;
            var fivePercentIndex = (int)Math.Floor((decimal)results.Ticks.Count * 5 / 100) + 1;
            var medianIndex = (int)Math.Floor((decimal)results.Ticks.Count * 50 / 100) + 1;
            var onePercentile = results.Ticks[results.Ticks.Count - onePercentIndex];
            var fivePercentile = results.Ticks[results.Ticks.Count - fivePercentIndex];
            var median = results.Ticks[results.Ticks.Count - medianIndex];

            lock (_lock)
            {
                if (!string.IsNullOrEmpty(configuration.Name))
                {
                    Console.WriteLine();
                    Console.WriteLine("[" + configuration.Name + "]");
                }

                Console.WriteLine("{0:N0} iterations in {1:N0} ms ({2:N1} iterations/sec)",
                                  configuration.Iteration,
                                  results.Elapsed.TotalMilliseconds,
                                  configuration.Iteration / results.Elapsed.TotalSeconds);

                Console.WriteLine("Latencies :");
                Console.WriteLine("Min :          {0,10:### ### ##0}µs", min * _µsFrequency);
                Console.WriteLine("Avg :          {0,10:### ### ##0}µs", (double)results.Elapsed.Ticks / configuration.Iteration / (TimeSpan.TicksPerMillisecond / 1000));
                Console.WriteLine("Median :       {0,10:### ### ##0}µs", median * _µsFrequency);
                Console.WriteLine("95 percentile : {0,10:### ### ##0}µs", fivePercentile * _µsFrequency);
                Console.WriteLine("99 percentile : {0,10:### ### ##0}µs", onePercentile * _µsFrequency);
                Console.WriteLine("Max :          {0,10:### ### ##0}µs (Iteration #{1})", max * _µsFrequency, results.MaxIterationIndex);
                Console.WriteLine("G0 : {0}", results.G0Count);
                Console.WriteLine("G1 : {0}", results.G1Count);
                Console.WriteLine("G2 : {0}", results.G2Count);
            }
        }

        private class BenchResults
        {
            public TimeSpan Elapsed;
            public int G0Count;
            public int G1Count;
            public int G2Count;
            public long MaxIterationIndex;
            public List<long> Ticks;
        }

        public static IDisposable Throughput(int count)
        {
            GC.Collect();

            var g0Count = GC.CollectionCount(0);
            var g1Count = GC.CollectionCount(1);
            var g2Count = GC.CollectionCount(2);
            var stopwatch = Stopwatch.StartNew();

            return new DisposableAction(() =>
            {
                stopwatch.Stop();
                g0Count = GC.CollectionCount(0) - g0Count;
                g1Count = GC.CollectionCount(1) - g1Count;
                g2Count = GC.CollectionCount(2) - g2Count;

                Console.WriteLine("Elapsed(ms):  {0,10:0.00}", stopwatch.Elapsed.TotalMilliseconds);
                Console.WriteLine("FPS:          {0,10:0.00}", count / stopwatch.Elapsed.TotalSeconds);
                Console.WriteLine("G0 : {0,6:0}", g0Count);
                Console.WriteLine("G1 : {0,6:0}", g1Count);
                Console.WriteLine("G2 : {0,6:0}", g2Count);
            });
        }
    }
}