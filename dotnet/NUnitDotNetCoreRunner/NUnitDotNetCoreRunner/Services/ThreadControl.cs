using System;
using System.Threading;
using System.Threading.Tasks;

namespace NUnitDotNetCoreRunner.Services
{
    public class ThreadControl : IThreadControl
    {
        private readonly SemaphoreSlim _taskExecution;

        private bool _enabled { get; set; }
        public double _throughput { get; }
        public int _iterations { get; }

        private int _actualIterations;

        public ThreadControl(double throughput, int iterations)
        {
            _taskExecution = new SemaphoreSlim(0);
            _throughput = throughput;
            _iterations = iterations;
            _enabled = throughput > 0;
            _actualIterations = 0;
        }

        public int ReleaseTaskExecution(int count = 1) 
        {
            return _enabled
                ? _taskExecution.Release(count)
                : 0;
        }

        public async Task RequestTaskExecution(CancellationToken ct)
        {
            if (_enabled) await _taskExecution.WaitAsync(ct);
            var actualIterations = Interlocked.Increment(ref _actualIterations);
            if (actualIterations >= _iterations)
            {
                throw new Exception("Iterations exceeded");
            }
        }

        public int RequestTokens()
        {
            return 1;
        }

        public bool IsTestCompleted()
        {
            return true;
        }
    }

    public interface IThreadControl
    {
        Task RequestTaskExecution(CancellationToken ct);
        int ReleaseTaskExecution(int count = 1);
    }
}
