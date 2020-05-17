using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NUnitDotNetCoreRunner.Services
{
    public class ThreadAllocator : IThreadAllocator
    {
        private readonly INUnitAdapter _nunit;
        private readonly IReportWriter _reportWriter;
        private readonly IThreadControl _threadControl;
        private readonly CancellationTokenSource _testCts;
        private readonly CancellationTokenSource _reportWriterCts;
        private readonly List<Task> _tasks;
        private DateTime _startTime { get; set; } = DateTime.UtcNow;

        public ThreadAllocator(
            IReportWriter reportWriter, 
            IThreadControl threadControl, 
            INUnitAdapter nUnitAdapter)
        {
            _reportWriter = reportWriter;
            _threadControl = threadControl;
            _nunit = nUnitAdapter;
            _testCts = new CancellationTokenSource();
            _reportWriterCts = new CancellationTokenSource();
            _tasks = new List<Task>();
        }

        public async Task Run(
            int concurrency,
            double throughput,
            int rampUpMinutes,
            int holdMinutes,
            int iterations)
        {
            if (iterations <= 0) 
            {
                _testCts.CancelAfter(TestDuration(rampUpMinutes, holdMinutes));
            }
            _startTime = DateTime.UtcNow;

            try
            {
                var reportWriterTask = Task.Run(() => _reportWriter.StartWriting(), _reportWriterCts.Token);
                var tasks = new Task[]
                {
                Task.Run(() => StartThreads(concurrency, rampUpMinutes), _testCts.Token),
                Task.Run(() => ReleaseTaskExecutions(throughput, iterations, rampUpMinutes, holdMinutes), _testCts.Token)
                };
                await Task.WhenAll(tasks);
                await Task.WhenAll(_tasks);
                _reportWriter.TestsCompleted = true;
                _reportWriterCts.CancelAfter(TimeSpan.FromSeconds(30));
                await reportWriterTask;
            }
            catch (TaskCanceledException){ }
            catch (AggregateException e) when (e.InnerExceptions.All(x => x is TaskCanceledException)){ }
        } 

        private async Task StartThreads(int concurrency, int rampUpMinutes)
        {
            var threadsRemaining = concurrency;
            while (InRampup(concurrency, rampUpMinutes))
            {
                var sleepInterval = TimeSpan.FromSeconds(rampUpMinutes * 60 / concurrency);
                StartThread();
                threadsRemaining--;
                await Task.Delay(sleepInterval, _testCts.Token);
            }
            Parallel.For(0, threadsRemaining, _ => StartThread());
        }

        private void StartThread() =>
            _tasks.Add(Task.Run(() => _nunit.RunTest($"worker_{Guid.NewGuid().ToString("N")}", _testCts.Token)));

        private async Task ReleaseTaskExecutions(double throughput, int iterations, int rampUpMinutes, int holdMinutes)
        {
            if (throughput <= 0)
            {
                return;
            }

            TimeSpan sleepInterval;
            int tokensPerInterval;
            var partialTokens = 0d;
            var partialTokensPerInterval = 0d;
            if (throughput > 1)
            {
                sleepInterval = TimeSpan.FromSeconds(1 / throughput);
                tokensPerInterval = 1;
            }
            else
            {
                sleepInterval = TimeSpan.FromSeconds(1);
                tokensPerInterval = (int)throughput;
                partialTokensPerInterval = Math.Truncate(throughput);
            }

            int tokenTotal = 0;
            while (!IsTestComplete(tokenTotal, iterations, rampUpMinutes, holdMinutes) 
                && !_testCts.Token.IsCancellationRequested)
            {
                var accumulatedTokens = tokensPerInterval * PercentageThroughRampUp(rampUpMinutes)
                    + partialTokensPerInterval * PercentageThroughRampUp(rampUpMinutes)
                    + partialTokens;

                partialTokens = Math.Truncate(accumulatedTokens);
                
                if (iterations > 0 && tokenTotal + (int)accumulatedTokens > iterations)
                {
                    accumulatedTokens = iterations - tokenTotal;
                }

                if (int.MaxValue - accumulatedTokens < tokenTotal)
                {
                    tokenTotal += (int)accumulatedTokens;
                }

                _threadControl.ReleaseTaskExecution((int)accumulatedTokens);
                await Task.Delay(sleepInterval, _testCts.Token);
            }
        }

        private bool InRampup(int concurrency, int rampUpMinutes) => 
            concurrency > 1 && _startTime.AddMinutes(rampUpMinutes) < DateTime.UtcNow;
        private double SecondsFromStart => _startTime.Subtract(DateTime.UtcNow).TotalSeconds;
        private double PercentageThroughRampUp(int rampUpMinutes) => 
            SecondsFromStart / TimeSpan.FromMinutes(rampUpMinutes).TotalSeconds;

        private TimeSpan TestDuration (int rampUpMinutes, int holdMinutes) => 
            TimeSpan.FromMinutes(rampUpMinutes + holdMinutes);

        private bool IsTestComplete(int iterations, int configuredIterations, int rampUpMinutes, int holdMinutes) =>
            (configuredIterations > 0 && iterations >= configuredIterations)
            || DateTime.UtcNow > _endTime(rampUpMinutes, holdMinutes);

        private DateTime _endTime (int rampUpMinutes, int holdMinutes) => 
            _startTime.AddMinutes(rampUpMinutes + holdMinutes);
    }

    public interface IThreadAllocator
    {
        Task Run(
            int concurrency,
            double throughput,
            int rampUpMinutes,
            int holdMinutes,
            int iterations);
    }
}
