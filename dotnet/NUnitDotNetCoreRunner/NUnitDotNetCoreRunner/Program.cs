using CommandLine;
using NUnitDotNetCoreRunner.Models;
using NUnitDotNetCoreRunner.Services;
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
                    PrintConfig(o);
                    var reportItems = new ConcurrentQueue<ReportItem>();
                    var reportWriter = new ReportWriter(reportItems, o.ReportFile);
                    var threadControl = new ThreadControl(o.Throughput, o.Iterations, o.RampUpMinutes * 60, o.HoldForMinutes * 60);
                    var nunitAdapter = new NUnitAdapter(o.TargetAssembly, o.TestName, threadControl, reportItems);
                    var threadAllocator = new ThreadAllocator(reportWriter, threadControl, nunitAdapter);
                    await threadAllocator.Run(o.Concurrency, o.Throughput, o.RampUpMinutes * 60, o.HoldForMinutes * 60);
                });
        }

        private static void PrintConfig(RunnerOptions o)
        {
            Console.WriteLine($"Report file: {o.ReportFile}");
            Console.WriteLine($"Concurrent users: {o.Concurrency}");
            if (o.Throughput > 0)
            {
                Console.WriteLine($"Test: {o.Throughput}");
            }
            if (o.Iterations > 0)
            {
                Console.WriteLine($"Iterations: {o.Iterations}");
            }
            if (o.RampUpMinutes > 0)
            {
                Console.WriteLine($"Ramp period: {o.RampUpMinutes}");
            }
            Console.WriteLine($"Hold for: {o.HoldForMinutes}");
            Console.WriteLine($"Target: {o.TargetAssembly}");
            Console.WriteLine($"Test: {o.TestName ?? "<all>"}");
        }
    }
}
