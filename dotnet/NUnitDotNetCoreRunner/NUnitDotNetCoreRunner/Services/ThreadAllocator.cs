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
        private DateTime _startTime { get; set; }

        public ThreadAllocator(INUnitAdapter nUnitAdapter, IThreadControl threadControl)
        {
            _tasks = new List<Task>();
            _nUnitAdapter = nUnitAdapter;
            _threadControl = threadControl;
        }

        public async Task StartThreads(int concurrency, int rampUpSeconds, CancellationToken ct)
        {
            //maybe todo - request desired thread state from thread control and adjust to match
            var threadsRemaining = concurrency;
            _startTime = DateTime.Now;
            while (InRampup(concurrency, rampUpSeconds) && !ct.IsCancellationRequested)
            {
                var sleepInterval = TimeSpan.FromSeconds(rampUpSeconds / concurrency);
                if (sleepInterval.TotalSeconds > rampUpSeconds)
                {
                    sleepInterval = TimeSpan.FromSeconds(rampUpSeconds);
                }

                if (StartTask(concurrency, ct))
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
                if (!StartTask(concurrency, ct))
                {
                    break;
                }
            }
        }

        private bool StartTask(int concurrency, CancellationToken ct)
        {
            if (_tasks.Count < concurrency)
            {
                _tasks.Add(Task.Run(() => TestLoop(ct), ct));
                return true;
            }
            return false;
        }

        private async Task TestLoop(CancellationToken ct)
        {
            var threadName = $"worker_{Guid.NewGuid().ToString("N")}";
            while (!ct.IsCancellationRequested)
            {
                var canRun = false;
                try
                {
                    canRun = await _threadControl.RequestTaskExecution(ct);
                    if (!ct.IsCancellationRequested && canRun)
                    {
                        _nUnitAdapter.RunTest(threadName);
                    }
                }
                finally
                {
                    if (canRun)
                    {
                        _threadControl.ReleaseTaskExecution();
                    }
                }
            };
        }

        private bool InRampup(int concurrency, int rampUpSeconds) =>
            concurrency > 1
            && rampUpSeconds > 1
            && _startTime.AddSeconds(rampUpSeconds) > DateTime.UtcNow;
    }

    public interface IThreadAllocator
    {
        Task StartThreads(int concurrency, int rampUpSeconds, CancellationToken ct);
    }
}
