using System.Threading;
using System.Threading.Tasks;

namespace NUnitDotNetCoreRunner.Services
{
    public class ThreadControl : IThreadControl
    {
        private readonly SemaphoreSlim _taskExecution;

        private bool _enabled { get; set; }
        public ThreadControl(bool enabled)
        {
            _taskExecution = new SemaphoreSlim(0);
            _enabled = enabled;
        }

        public int ReleaseTaskExecution(int count = 1) => _enabled
            ? _taskExecution.Release(count)
            : 0;

        public async Task RequestTaskExecution(CancellationToken ct)
        {
            if (_enabled) await _taskExecution.WaitAsync(ct);
        }
    }

    interface IThreadControl
    {
        Task RequestTaskExecution(CancellationToken ct);
        int ReleaseTaskExecution(int count = 1);
    }
}
