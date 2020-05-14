using CommandLine;
using NUnit.Engine;
using NUnitRunner.Models;
using NUnitRunner.Services;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NUnitRunner
{
    class Program
    {
        public static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();
        }

        private static async Task MainAsync(string[] args)
        {
            await Parser.Default
                .ParseArguments<RunnerOptions>(args)
                .WithParsedAsync(async o =>
                {
                    if (o.TargetAssembly == null)
                    {
                        throw new Exception("Target test suite wasn't provided. Is your file actually NUnit test DLL?");
                    }

                    if (o.Concurrency <= 0)
                    {
                        o.Concurrency = 1;
                    }

                    o.DurationLimit = o.Hold + o.RampUp;

                    if (o.DurationLimit == 0 && o.Iterations == 0)
                    {
                        o.Iterations = 1;
                    }

                    Console.WriteLine($"Concurrent users: {o.Concurrency}");
                    Console.WriteLine($"Iterations: {o.Iterations}");
                    Console.WriteLine($"Ramp period: {o.RampUp}");
                    Console.WriteLine($"Hold for: {o.Hold}");
                    Console.WriteLine($"Report file: {o.ReportFile}");
                    Console.WriteLine($"Target: {o.TargetAssembly}");

                    await Execute(o);
                });
        }

        private static async Task Execute(RunnerOptions options)
        {
            var engine = TestEngineActivator.CreateInstance();
            var package = new TestPackage(options.TargetAssembly);

            var reportItems = new ConcurrentQueue<ReportItem>();
            var testEventListener = new TestEventListener(engine, package, reportItems, string.Empty);

            var testCount = testEventListener.Runner.CountTestCases(TestFilter.Empty);
            if (testCount == 0)
            {
                throw new ArgumentException("Nothing to run, no tests were loaded");
            }

            var userStepTime = options.RampUp / options.Concurrency;
            var reportWriter = new ReportWriter(reportItems);
            var reportWriterTask = Task.Run(() => reportWriter.StartWriting(options.ReportFile));
            var startTime = DateTime.UtcNow;
            var testTasks = new Task[options.Concurrency];

            for (int i = 0; i < options.Concurrency; i++)
            {
                var threadName = "worker_" + (i + 1);
                testTasks[i] = Task.Run(() => Test.RunTest(
                    startTime, options, new TestEventListener(engine, package, reportItems, threadName)));
                Thread.Sleep(userStepTime * 1000);
            }

            await Task.WhenAll(testTasks);
            reportWriter.TestsCompleted = true;
            await reportWriterTask;
        }
    }
}
