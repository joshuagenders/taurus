using CommandLine;
using NUnitDotNetCoreRunner.Services;
using NUnitRunner.Models;
using NUnitRunner.Services;
using System;
using System.Collections.Concurrent;
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
                    Console.WriteLine($"Concurrent users: {o.Concurrency}");
                    Console.WriteLine($"Iterations: {o.Iterations}");
                    Console.WriteLine($"Ramp period: {o.RampUpMinutes}");
                    Console.WriteLine($"Hold for: {o.HoldMinutes}");
                    Console.WriteLine($"Report file: {o.ReportFile}");
                    Console.WriteLine($"Target: {o.TargetAssembly}");
                    var reportItems = new ConcurrentQueue<ReportItem>();
                    var reportWriter = new ReportWriter(reportItems, o.ReportFile);
                    var threadControl = new ThreadControl(o.Throughput > 0);
                    var nunitAdapter = new NUnitAdapter(o, threadControl, reportItems);
                    var threadAllocator = new ThreadAllocator(reportWriter, threadControl, nunitAdapter);
                    await threadAllocator.Run(o.Concurrency, o.Throughput, o.RampUpMinutes, o.HoldMinutes, o.Iterations);
                });
        }
    }
}
