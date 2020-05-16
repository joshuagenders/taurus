using CommandLine;
using NUnitDotNetCoreRunner.Services;
using NUnitRunner.Models;
using System;
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
                    Console.WriteLine($"Ramp period: {o.RampUp}");
                    Console.WriteLine($"Hold for: {o.Hold}");
                    Console.WriteLine($"Report file: {o.ReportFile}");
                    Console.WriteLine($"Target: {o.TargetAssembly}");

                    var threadAllocator = new ThreadAllocator(o);
                    await threadAllocator.Run();
                });
        }
    }
}
