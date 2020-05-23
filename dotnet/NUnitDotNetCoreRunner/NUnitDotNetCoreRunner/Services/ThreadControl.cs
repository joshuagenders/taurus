using System;
using System.Threading;
using System.Threading.Tasks;

namespace NUnitDotNetCoreRunner.Services
{
    public class ThreadControl : IThreadControl
    {
        private readonly SemaphoreSlim _taskExecution;
        private readonly SemaphoreSlim _taskIncrement;

        private bool _enabled { get; set; }
        public double _throughput { get; }
        public int _iterations { get; }
        public int _rampUpSeconds { get; }
        public int _holdForSeconds { get; }

        private int _executionRequestCount;

        public ThreadControl(double throughput, int iterations, int rampUpSeconds, int holdForSeconds)
        {
            _taskExecution = new SemaphoreSlim(0);
            _taskIncrement = new SemaphoreSlim(1);
            _throughput = throughput;
            _iterations = iterations;
            _rampUpSeconds = rampUpSeconds;
            _holdForSeconds = holdForSeconds;
            _enabled = throughput > 0;
            _executionRequestCount = 0;
        }

        public int ReleaseTaskExecution(int count = 1) =>
            _enabled
                ? _taskExecution.Release(count)
                : 0;

        public async Task<bool> RequestTaskExecution(DateTime startTime, CancellationToken ct)
        {
            if (_enabled)
            {
                await _taskExecution.WaitAsync(ct);
            }
            int iterations;
            try
            {
                await _taskIncrement.WaitAsync(ct);
                iterations = Interlocked.Increment(ref _executionRequestCount);
            }
            finally
            {
                if (_taskIncrement.CurrentCount <= 0)
                {
                    _taskIncrement.Release();
                }
            }

            var e = EndTime(startTime, _rampUpSeconds, _holdForSeconds);
            var t = IsTestComplete(e, iterations);
            if (iterations > 5)
            {
                Console.WriteLine();
            }
            return t;
        }

        private int TotalAllowedRequestsToNow(int millisecondsEllapsed)
        {
            if (millisecondsEllapsed <= 0 || _throughput <= 0) { 
                return 0;
            }
            double totalRpsToNow;
            var secondsEllapsed = Convert.ToInt32(millisecondsEllapsed / 1000);
            if (_rampUpSeconds > 0)
            {
                if (secondsEllapsed > _rampUpSeconds)
                {
                    totalRpsToNow = (_throughput * _rampUpSeconds / 2)
                        + (secondsEllapsed - _rampUpSeconds) * _throughput;
                }
                else
                {
                    totalRpsToNow = (_throughput * millisecondsEllapsed) / 1000 / 2;
                }
            }
            else
            {
                totalRpsToNow = (_throughput * millisecondsEllapsed) / 1000;
            }
            return Convert.ToInt32(totalRpsToNow);
        }

        public async Task ReleaseTokens(DateTime startTime, CancellationToken ct)
        {
            if (!_enabled)
            {
                return;
            }
            var tokensReleased = 0;
            var endTime = EndTime(startTime, _rampUpSeconds, _holdForSeconds);
            while (!IsTestComplete(endTime, tokensReleased) && !ct.IsCancellationRequested)
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
                        System.Diagnostics.Debug.WriteLine($"tokensReleased {tokensToRelease}");
                        tokensReleased += tokensToRelease;
                        _taskExecution.Release(tokensToRelease);
                    }
                }
                System.Diagnostics.Debug.WriteLine($"totaltokensReleased {tokensReleased}");
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            }
        }

        private bool IsTestComplete(DateTime endTime, int iterations) => 
            DateTime.UtcNow >= endTime || IterationsExceeded(iterations);

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
