using NUnitDotNetCoreRunner.Models;
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
            int holdForSeconds)
        {
            await _executionSemaphore.WaitAsync();
            _testCts.CancelAfter(TestDuration(rampUpSeconds, holdForSeconds));
            _startTime = DateTime.UtcNow;

            try
            {
                var reportWriterTask = Task.Run(() => _reportWriter.StartWriting(), _reportWriterCts.Token);
                var tasks = new List<Task>
                {
                    Task.Run(() => StartThreads(concurrency, rampUpSeconds, _testCts.Token), _testCts.Token)
                };
                if (throughput > 0)
                {
                    tasks.Add(Task.Run(() => ReleaseTaskExecutions(), _testCts.Token));
                }

                await Task.WhenAll(tasks);
                await Task.WhenAll(_tasks);
                _reportWriter.TestsCompleted = true;
                _reportWriterCts.CancelAfter(TimeSpan.FromSeconds(30));
                await reportWriterTask;
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
            catch (IterationsExceededException) { }
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
                _tasks.Add(Task.Run(() => StartTestLoop(), _testCts.Token));
            }
        }

        private async Task StartTestLoop()
        {
            while (!_testCts.IsCancellationRequested)
            {
                try
                {
                    await _threadControl.RequestTaskExecution(_testCts.Token);
                    if (!_testCts.IsCancellationRequested)
                    {
                        System.Diagnostics.Debug.WriteLine($"Running test");
                        await _nunit.RunTest($"worker_{Guid.NewGuid().ToString("N")}", _testCts.Token);
                        System.Diagnostics.Debug.WriteLine($"Finished running test");
                    }
                }
                catch (IterationsExceededException)
                {
                    break;
                }
                finally
                {
                    _threadControl.ReleaseTaskExecution();
                }
            };
        }

        private async Task ReleaseTaskExecutions()
        {
            var tokens = 0d;
            while (!_threadControl.IsTestComplete() && !_testCts.Token.IsCancellationRequested)
            {
                System.Diagnostics.Debug.WriteLine("ReleaseTaskExecution Iteration Start");

                tokens += _threadControl.RequestTokens();
                var tokensToRelease = (int)Math.Floor(tokens);
                tokens = Math.Truncate(tokens);
                if (tokensToRelease > 0)
                {
                    _threadControl.ReleaseTaskExecution(tokensToRelease);

                    System.Diagnostics.Debug.WriteLine($"Tokens to release: {tokensToRelease}");
                }
                await Task.Delay(TimeSpan.FromSeconds(1), _testCts.Token);

                System.Diagnostics.Debug.WriteLine("ReleaseTaskExecution Iteration End");
            }
        }

        private bool InRampup(int concurrency, int rampUpSeconds) => 
            concurrency > 1 
            && rampUpSeconds > 1 
            && _startTime.AddSeconds(rampUpSeconds) < DateTime.UtcNow;

        private TimeSpan TestDuration (int rampUpSeconds, int holdForSeconds) => 
            TimeSpan.FromSeconds(rampUpSeconds + holdForSeconds);
    }

    public interface IThreadAllocator
    {
        Task Run(
            int concurrency,
            double throughput,
            int rampUpSeconds,
            int holdForSeconds);
    }
}
