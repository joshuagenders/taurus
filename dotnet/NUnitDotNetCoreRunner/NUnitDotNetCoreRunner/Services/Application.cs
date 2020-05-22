using NUnitDotNetCoreRunner.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NUnitDotNetCoreRunner.Services
{
    public class Application : IApplication
    {
        private readonly IReportWriter _reportWriter;
        private readonly IThreadControl _threadControl;
        private readonly IThreadAllocator _threadAllocator;
        private readonly CancellationTokenSource _testCts;
        private readonly CancellationTokenSource _reportWriterCts;
        private readonly List<Task> _tasks;
        private readonly SemaphoreSlim _executionSemaphore;

        public Application(
            IReportWriter reportWriter,
            IThreadAllocator threadAllocator,
            IThreadControl threadControl)
        {
            _reportWriter = reportWriter;
            _threadControl = threadControl;
            _threadAllocator = threadAllocator;
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
            var startTime = DateTime.UtcNow;
            _testCts.CancelAfter(TestDuration(rampUpSeconds, holdForSeconds));
            
            try
            {
                var reportWriterTask = Task.Run(() => _reportWriter.StartWriting(), _reportWriterCts.Token);
                var tasks = new List<Task>
                {
                    Task.Run(() => _threadAllocator.StartThreads(startTime, concurrency, rampUpSeconds, _testCts.Token), _testCts.Token),
                    Task.Run(() => Task.Delay(TestDuration(rampUpSeconds, holdForSeconds), _testCts.Token))
                };
                if (throughput > 0)
                {
                    tasks.Add(Task.Run(() => _threadControl.ReleaseTokens(startTime, _testCts.Token), _testCts.Token));
                }

                await Task.WhenAll(tasks);
                await Task.WhenAll(_tasks);
                _reportWriter.TestsCompleted = true;
                _reportWriterCts.CancelAfter(TimeSpan.FromSeconds(5));
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

        private TimeSpan TestDuration (int rampUpSeconds, int holdForSeconds) => 
            TimeSpan.FromSeconds(rampUpSeconds + holdForSeconds);
    }

    public interface IApplication
    {
        Task Run(
            int concurrency,
            double throughput,
            int rampUpSeconds,
            int holdForSeconds);
    }
}
