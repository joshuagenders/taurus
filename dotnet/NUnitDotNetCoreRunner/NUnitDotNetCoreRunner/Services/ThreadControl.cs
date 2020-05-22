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
        public int _rampUpSeconds { get; }
        public int _holdForSeconds { get; }

        private int _executionRequestCount;

        private static Mutex _mutex = new Mutex();

        public ThreadControl(double throughput, int iterations, int rampUpSeconds, int holdForSeconds)
        {
            _taskExecution = new SemaphoreSlim(0);
            _throughput = throughput;
            _iterations = iterations;
            _rampUpSeconds = rampUpSeconds;
            _holdForSeconds = holdForSeconds;
            _enabled = throughput > 0;
            _executionRequestCount = 0;
        }

        public int ReleaseTaskExecution(int count = 1) 
        {
            return _enabled
                ? _taskExecution.Release(count)
                : 0;
        }

        public async Task<bool> RequestTaskExecution(DateTime startTime, CancellationToken ct)
        {
            if (_enabled) await _taskExecution.WaitAsync(ct);
            int iterations;
            try
            {
                _mutex.WaitOne();
                iterations = Interlocked.Increment(ref _executionRequestCount);
            }
            finally
            {
                _mutex.ReleaseMutex();
            }
            return IsTestComplete(startTime, iterations);
        }

        private int TotalAllowedRequestsToNow(int millisecondsEllapsed)
        {
            if (millisecondsEllapsed <= 0 || _throughput <= 0) { 
                return 0;
            }
            double totalRpsToNow;
            var secondsEllapsed = Convert.ToInt32(millisecondsEllapsed / 1000);
            if (_rampUpSeconds > 0 && secondsEllapsed > _rampUpSeconds)
            {
                totalRpsToNow = (_throughput * _rampUpSeconds / 2)
                    + (secondsEllapsed - _rampUpSeconds) * _throughput;
            }
            else
            {
                totalRpsToNow = (_throughput * millisecondsEllapsed) / 1000 / 2;
            }
            return Convert.ToInt32(totalRpsToNow);
        }

        public async Task ReleaseTokens(DateTime startTime, CancellationToken ct)
        {
            var tokensReleased = 0;
            while (!DurationComplete(startTime) && !ct.IsCancellationRequested)
            {
                var millisecondsEllapsed = Convert.ToInt32(DateTime.UtcNow.Subtract(startTime).TotalMilliseconds);
                var tokensToNow = TotalAllowedRequestsToNow(millisecondsEllapsed);

                if (tokensToNow > tokensReleased)
                {
                    var tokensToRelease = Convert.ToInt32(tokensToNow - tokensReleased);
                    if (_iterations > 0)
                    {
                        if (int.MaxValue - tokensToRelease < tokensReleased)
                        {
                            tokensToRelease = int.MaxValue - tokensReleased;
                        }
                        if (tokensToRelease + tokensReleased > _iterations)
                        {
                            tokensToRelease = _iterations - tokensReleased;
                        }
                    }
                    if (tokensToRelease > 0)
                    {
                        tokensReleased += tokensToRelease;
                        _taskExecution.Release(tokensToRelease);
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            }
        }

        private bool IsTestComplete(DateTime startTime, int iterations) => 
            DurationComplete(startTime) || IterationsExceeded(iterations);

        private bool DurationComplete(DateTime startTime) => 
            DateTime.UtcNow >= EndTime(startTime, _rampUpSeconds, _holdForSeconds);

        private bool IterationsExceeded(int iterations) =>
            _iterations > 0 && iterations > _iterations;

        private DateTime EndTime(DateTime startTime, int rampUpSeconds, int holdForSeconds) =>
            startTime.AddSeconds(rampUpSeconds + holdForSeconds);
    }

    public interface IThreadControl
    {
        Task<bool> RequestTaskExecution(DateTime startTime, CancellationToken ct);
        int ReleaseTaskExecution(int count = 1);
        Task ReleaseTokens(DateTime startTime, CancellationToken ct);
    }
}
