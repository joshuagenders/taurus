using NUnit.Engine;
using NUnitDotNetCoreRunner.Models;
using System;
using System.Collections.Concurrent;

namespace NUnitDotNetCoreRunner.Services
{
    public class NUnitAdapter : INUnitAdapter
    {
        private readonly ITestEngine _engine;
        private readonly TestPackage _package;
        private readonly ConcurrentQueue<ReportItem> _reportItems;
        private readonly TestFilter _filter;

        public NUnitAdapter(
            string targetAssembly, 
            string testName,
            ConcurrentQueue<ReportItem> reportItems)
        {
            _engine = TestEngineActivator.CreateInstance();
            _package = new TestPackage(targetAssembly);
            _reportItems = reportItems;
            
            _filter = string.IsNullOrWhiteSpace(testName)
                ? TestFilter.Empty
                : new TestFilter($"<filter><name>{testName}</name></filter>");

            var runner = _engine.GetRunner(_package);
            var testCount = runner.CountTestCases(_filter);
            if (testCount == 0)
            {
                throw new ArgumentException("Nothing to run, no tests were loaded");
            }
        }

        public void RunTest(string threadName)
        {
            var runner = _engine.GetRunner(_package);
            runner.Run(new TestEventListener(_reportItems, threadName), _filter);
        }
    }

    public interface INUnitAdapter
    {
        void RunTest(string threadName);
    }
}
