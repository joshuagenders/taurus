using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace NUnitDotNetCoreRunner.Services
{
    class ThreadControl
    {
        private readonly SemaphoreSlim _taskExecution;

        private bool _enabled { get; set; }
        public ThreadControl(bool enabled)
        {
            _taskExecution = new SemaphoreSlim(0);
            _enabled = enabled;
        }

        internal int ReleaseTaskExecution(int count = 1) => _enabled
            ? _taskExecution.Release(count)
            : 0;

        internal async Task RequestTaskExecution(CancellationToken ct)
        {
            if (_enabled) await _taskExecution.WaitAsync(ct);
        }
    }
}
