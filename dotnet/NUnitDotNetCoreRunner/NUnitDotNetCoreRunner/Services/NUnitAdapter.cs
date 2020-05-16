using NUnit.Engine;
using NUnitRunner.Models;
using NUnitRunner.Services;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace NUnitDotNetCoreRunner.Services
{
    public class NUnitAdapter : INUnitAdapter
    {
        private readonly RunnerOptions _options;
        private readonly ThreadControl _threadControl;
        private readonly ITestEngine _engine;
        private readonly TestPackage _package;
        private readonly ConcurrentQueue<ReportItem> _reportItems;
        private readonly TestFilter _filter;

        public NUnitAdapter(RunnerOptions options, ThreadControl threadControl, ConcurrentQueue<ReportItem> reportItems)
        {
            _options = options;
            _threadControl = threadControl;
            _engine = TestEngineActivator.CreateInstance();
            _package = new TestPackage(_options.TargetAssembly);
            _reportItems = reportItems;
            
            _filter = string.IsNullOrWhiteSpace(options.TestName)
                ? TestFilter.Empty
                : new TestFilter($"<filter><name>{options.TestName}</name></filter>");

            var runner = _engine.GetRunner(_package);
            var testCount = runner.CountTestCases(_filter);
            if (testCount == 0)
            {
                throw new ArgumentException("Nothing to run, no tests were loaded");
            }
        }

        public async Task RunTest(string threadName, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                await _threadControl.RequestTaskExecution(ct);
                var runner = _engine.GetRunner(_package);
                runner.Run(new TestEventListener(_reportItems, threadName), _filter);
                _threadControl.ReleaseTaskExecution();
            };
        }
    }

    public interface INUnitAdapter
    {
        Task RunTest(string threadName, CancellationToken ct);
    }
}
