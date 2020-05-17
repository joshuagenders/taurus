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
        private readonly SemaphoreSlim _executionSemaphore;
        private DateTime _startTime { get; set; } = DateTime.UtcNow;

        public ThreadAllocator(
            IReportWriter reportWriter,
            IThreadControl threadControl,
            INUnitAdapter nUnitAdapter)
        {
            _reportWriter = reportWriter;
            _threadControl = threadControl;
            _nunit = nUnitAdapter;
            _executionSemaphore = new SemaphoreSlim(1);
            _testCts = new CancellationTokenSource();
            _reportWriterCts = new CancellationTokenSource();
            _tasks = new List<Task>();
        }

        public async Task Run(
            int concurrency,
            double throughput,
            int rampUpSeconds,
            int holdForSeconds,
            int iterations)
        {
            await _executionSemaphore.WaitAsync();
            _testCts.CancelAfter(TestDuration(rampUpSeconds, holdForSeconds));
            _startTime = DateTime.UtcNow;

            try
            {
                var reportWriterTask = Task.Run(() => _reportWriter.StartWriting(), _reportWriterCts.Token);
                var tasks = new Task[]
                {
                Task.Run(() => StartThreads(concurrency, rampUpSeconds, _testCts.Token), _testCts.Token),
                Task.Run(() => ReleaseTaskExecutions(throughput, iterations, rampUpSeconds, holdForSeconds), _testCts.Token)
                };
                await Task.WhenAll(tasks);
                await Task.WhenAll(_tasks);
                _reportWriter.TestsCompleted = true;
                _reportWriterCts.CancelAfter(TimeSpan.FromSeconds(30));
                await reportWriterTask;
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
            catch (AggregateException e) when (e.InnerExceptions.All(x => x is TaskCanceledException || x is OperationCanceledException)) { }
            finally
            {
                _executionSemaphore.Release();
            }
        }

        private async Task StartThreads(int concurrency, int rampUpSeconds, CancellationToken ct)
        {
            //maybe todo - request desired thread state from thread control and adjust to match
            var threadsRemaining = concurrency;
            while (InRampup(concurrency, rampUpSeconds) && !ct.IsCancellationRequested)
            {
                var sleepInterval = TimeSpan.FromSeconds(rampUpSeconds / concurrency);
                _tasks.Add(Task.Run(() => StartTestLoop()));
                if (--threadsRemaining <= 0)
                {
                    break;
                }
                await Task.Delay(sleepInterval, _testCts.Token);
            }
            for (var i = 0; i < threadsRemaining; i++)
            {
                _tasks.Add(Task.Run(() => _nunit.RunTest($"worker_{Guid.NewGuid().ToString("N")}", _testCts.Token)));
            }
        }

        private async Task StartTestLoop()
        {
            while (!_testCts.IsCancellationRequested)
            {
                await _threadControl.RequestTaskExecution(_testCts.Token);
                try
                {
                    if (!_testCts.IsCancellationRequested)
                    {
                        await _nunit.RunTest($"worker_{Guid.NewGuid().ToString("N")}", _testCts.Token);
                    }
                }
                finally
                {
                    _threadControl.ReleaseTaskExecution();
                }
            };
        }

        private async Task ReleaseTaskExecutions(double throughput, int iterations, int rampUpSeconds, int holdForSeconds)
        {
            if (throughput <= 0)
            {
                return;
            }

            var tokenTotal = 0;
            var tokens = 0d;
            while (!IsTestComplete(tokenTotal, iterations, rampUpSeconds, holdForSeconds)
                && !_testCts.Token.IsCancellationRequested)
            {
                tokens += throughput * PercentageThroughRampUp(rampUpSeconds);
                var tokensToRelease = (int)tokens;
                tokens = Math.Truncate(tokens);
                if (tokenTotal + tokensToRelease > iterations)
                {
                    tokensToRelease = iterations - tokenTotal;
                }
                tokenTotal += tokensToRelease;

                _threadControl.ReleaseTaskExecution(tokensToRelease);
                await Task.Delay(TimeSpan.FromSeconds(1), _testCts.Token);
            }
        }

        private bool InRampup(int concurrency, int rampUpSeconds) => 
            concurrency > 1 
            && rampUpSeconds > 1 
            && _startTime.AddSeconds(rampUpSeconds) < DateTime.UtcNow;

        private double SecondsFromStart => _startTime.Subtract(DateTime.UtcNow).TotalSeconds;

        private double PercentageThroughRampUp(int rampUpSeconds) => 
            SecondsFromStart / rampUpSeconds;

        private TimeSpan TestDuration (int rampUpSeconds, int holdForSeconds) => 
            TimeSpan.FromSeconds(rampUpSeconds + holdForSeconds);

        private bool IsTestComplete(int iterations, int configuredIterations, int rampUpSeconds, int holdForSeconds) =>
            (configuredIterations > 0 && iterations >= configuredIterations)
            || DateTime.UtcNow > EndTime(rampUpSeconds, holdForSeconds);

        private DateTime EndTime (int rampUpSeconds, int holdForSeconds) => 
            _startTime.AddSeconds(rampUpSeconds + holdForSeconds);
    }

    public interface IThreadAllocator
    {
        Task Run(
            int concurrency,
            double throughput,
            int rampUpSeconds,
            int holdForSecons,
            int iterations);
    }
}
