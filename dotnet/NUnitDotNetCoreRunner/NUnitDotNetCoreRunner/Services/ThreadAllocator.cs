using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NUnitDotNetCoreRunner.Services
{
    public class ThreadAllocator : IThreadAllocator
    {
        private readonly INUnitAdapter _nUnitAdapter;
        private readonly IThreadControl _threadControl;
        private readonly List<Task> _tasks;

        public ThreadAllocator(INUnitAdapter nUnitAdapter, IThreadControl threadControl)
        {
            _tasks = new List<Task>();
            _nUnitAdapter = nUnitAdapter;
            _threadControl = threadControl;
        }

        public async Task StartThreads(DateTime startTime, int concurrency, int rampUpSeconds, CancellationToken ct)
        {
            //maybe todo - request desired thread state from thread control and adjust to match
            var threadsRemaining = concurrency;
            while (InRampup(startTime, concurrency, rampUpSeconds) && !ct.IsCancellationRequested)
            {
                var sleepInterval = TimeSpan.FromSeconds(rampUpSeconds / concurrency);
                if (sleepInterval.TotalSeconds > rampUpSeconds)
                {
                    sleepInterval = TimeSpan.FromSeconds(rampUpSeconds);
                }

                if (StartTask(startTime, concurrency, ct))
                {
                    if (--threadsRemaining <= 0)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
                await Task.Delay(sleepInterval, ct);
            }
            for (var i = 0; i < threadsRemaining; i++)
            {
                if (!StartTask(startTime, concurrency, ct))
                {
                    break;
                }
            }
        }

        private bool StartTask(DateTime startTime, int concurrency, CancellationToken ct)
        {
            if (_tasks.Count < concurrency)
            {
                _tasks.Add(Task.Run(() => TestLoop(startTime, ct), ct));
                return true;
            }
            return false;
        }

        private async Task TestLoop(DateTime startTime, CancellationToken ct)
        {
            var threadName = $"worker_{Guid.NewGuid().ToString("N")}";
            bool iterationsExceeded = false;
            while (!ct.IsCancellationRequested && !iterationsExceeded)
            {
                try
                {
                    iterationsExceeded = await _threadControl.RequestTaskExecution(ct);
                    if (!ct.IsCancellationRequested && !iterationsExceeded)
                    {
                        _nUnitAdapter.RunTest(threadName);
                    }
                }
                finally
                {
                    _threadControl.ReleaseTaskExecution();
                }
            };
        }

        private bool InRampup(DateTime startTime, int concurrency, int rampUpSeconds) =>
            concurrency > 1
            && rampUpSeconds > 1
            && startTime.AddSeconds(rampUpSeconds) > DateTime.UtcNow;
    }

    public interface IThreadAllocator
    {
        Task StartThreads(DateTime startTime, int concurrency, int rampUpSeconds, CancellationToken ct);
    }
}
