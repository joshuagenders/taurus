using CommandLine;

namespace NUnitDotNetCoreRunner.Models
{
    public class RunnerOptions
    {
        [Option('f', "report-file", Required = false, HelpText = "Name of report file", Default = "report.ldjson")]
        public string ReportFile { get; set; }

        [Option('i', "iterations", Required = false, HelpText = "number of iterations over test suite to make")]
        public int Iterations { get; set; }

        [Option('d', "hold-for", Required = false, HelpText = "the number of minutes to hold execution for")]
        public int HoldMinutes { get; set; }

        [Option('c', "concurrency", Required = false, HelpText = "number of concurrent users (threads allocated)", Default = 1)]
        public int Concurrency { get; set; }

        [Option('r', "ramp_up_seconds", Required = false, HelpText = "time to ramp all concurrent users")]
        public int RampUpMinutes { get; set; }

        [Option('t', "throughput", Required = false, HelpText = "the target tests per second")]
        public double Throughput { get; set; }

        [Option('a', "assembly", Required = true, HelpText = "assembly which will be used to load tests from")]
        public string TargetAssembly { get; set; }

        [Option('n', "testname", Required = true, HelpText = "test name to pass into the NUnit '<name>' TestFilter")]
        public string TestName { get; set; }
    }
}
