using CommandLine;

namespace NUnitRunner.Models
{
    public class RunnerOptions
    {
        public bool Verbose { get; set; }

        [Option('r', "report-file", Required = false, HelpText = "Name of report file", Default = "report.ldjson")]
        public string ReportFile { get; set; }

        [Option('i', "iterations", Required = false, HelpText = "number of iterations over test suite to make")]
        public int Iterations { get; set; }

        [Option('f', "hold-for", Required = false, HelpText = "the number of minutes to hold execution for")]
        public int Hold { get; set; }

        [Option('d', "duration", Required = false, HelpText = "duration limit of test suite execution")]
        public int DurationLimit { get; set; }

        [Option('c', "concurrency", Required = false, HelpText = "number of concurrent users")]
        public int Concurrency { get; set; }

        [Option('l', "ramp_up", Required = false, HelpText = "time to ramp all concurrent users")]
        public int RampUp { get; set; }

        [Option('t', "target", Required = false, HelpText = "assembly which will be used to load tests from")]
        public string TargetAssembly { get; set; }
    }
}
