using NUnitRunner.Models;
using NUnitRunner.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NUnitDotNetCoreRunner.Services
{
    class ThreadAllocator
    {
        private readonly TestRunner _runner;
        private readonly ReportWriter _reportWriter;
        private readonly RunnerOptions _options;
        private readonly ThreadControl _threadControl;
        private readonly ConcurrentQueue<ReportItem> _reportItems;
        private readonly CancellationTokenSource _cts;
        private readonly List<Task> _tasks;
        private DateTime _startTime { get; set; } = DateTime.UtcNow;

        public ThreadAllocator(RunnerOptions options)
        {
            _options = options;
            _threadControl = new ThreadControl(options.Throughput > 0);
            _reportItems = new ConcurrentQueue<ReportItem>();
            _runner = new TestRunner(options, _threadControl, _reportItems);
            _reportWriter = new ReportWriter(_reportItems);
            _cts = new CancellationTokenSource();
            _tasks = new List<Task>();
        }

        public async Task Run()
        {
            _cts.CancelAfter(TestDuration);
            _startTime = DateTime.UtcNow;

            var reportWriterTask = Task.Run(() => _reportWriter.StartWriting(_options.ReportFile), _cts.Token);
            var tasks = new Task[]
            {
                Task.Run(() => StartThreads(), _cts.Token),
                Task.Run(() => ReleaseTaskExecutions(), _cts.Token)
            };
            await Task.WhenAll(tasks);
            await Task.WhenAll(_tasks);
            _reportWriter.TestsCompleted = true;
            await reportWriterTask;
        }

        private async Task StartThreads()
        {
            var threadsRemaining = _options.Concurrency;
            while (InRampup)
            {
                //todo go through and change rampup to seconds/minutes consistently
                var sleepInterval = TimeSpan.FromSeconds(_options.RampUp / _options.Concurrency);
                StartThread();
                threadsRemaining--;
                await Task.Delay(sleepInterval, _cts.Token);
            }
            Parallel.For(0, threadsRemaining, _ => StartThread());
        }

        private void StartThread() =>
            _tasks.Add(Task.Run(() => _runner.RunTest($"worker_{Guid.NewGuid().ToString("N")}", _cts.Token)));

        private async Task ReleaseTaskExecutions()
        {
            if (_options.Throughput <= 0)
            {
                return;
            }

            TimeSpan sleepInterval;
            int tokensPerInterval;
            var partialTokens = 0d;
            var partialTokensPerInterval = 0d;
            if (_options.Throughput > 1)
            {
                sleepInterval = TimeSpan.FromSeconds(1 / _options.Throughput);
                tokensPerInterval = 1;
            }
            else
            {
                sleepInterval = TimeSpan.FromSeconds(1);
                tokensPerInterval = (int)_options.Throughput;
                partialTokensPerInterval = Math.Truncate(_options.Throughput);
            }

            int tokenTotal = 0;
            while (!IsTestComplete(tokenTotal) && !_cts.Token.IsCancellationRequested)
            {
                var accumulatedTokens = tokensPerInterval * PercentageThroughRampUp
                    + partialTokensPerInterval * PercentageThroughRampUp 
                    + partialTokens;

                partialTokens = Math.Truncate(accumulatedTokens);
                
                if (_options.Iterations > 0 && tokenTotal + (int)accumulatedTokens > _options.Iterations)
                {
                    accumulatedTokens = _options.Iterations - tokenTotal;
                }

                if (int.MaxValue - accumulatedTokens < tokenTotal)
                {
                    tokenTotal += (int)accumulatedTokens;
                }

                _threadControl.ReleaseTaskExecution((int)accumulatedTokens);
                await Task.Delay(sleepInterval, _cts.Token);
            }
        }

        private bool InRampup => 
            _options.Concurrency > 1 && _startTime.AddMinutes(_options.RampUp) < DateTime.UtcNow;
        private double SecondsFromStart => _startTime.Subtract(DateTime.UtcNow).TotalSeconds;
        private double PercentageThroughRampUp => SecondsFromStart / TimeSpan.FromMinutes(_options.RampUp).TotalSeconds;

        private TimeSpan TestDuration => TimeSpan.FromMinutes(_options.RampUp + _options.Hold);

        private bool IsTestComplete(int iterations) =>
            (_options.Iterations > 0 && iterations >= _options.Iterations)
            || DateTime.UtcNow > _endTime;

        private DateTime _endTime => _startTime.AddMinutes(_options.RampUp + _options.Hold);
    }
}
