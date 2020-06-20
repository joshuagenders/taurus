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

        public async Task<bool> RequestTaskExecution(DateTime startTime, CancellationToken ct)
        {
            System.Diagnostics.Debug.WriteLine($"{DateTime.UtcNow.ToString("H:mm:ss.fff")} - task execution request start");
            if (_enabled)
            {
                System.Diagnostics.Debug.WriteLine($"{DateTime.UtcNow.ToString("H:mm:ss.fff")} - task execution enabled, waiting");
                await _taskExecution.WaitAsync(ct);
                System.Diagnostics.Debug.WriteLine($"{DateTime.UtcNow.ToString("H:mm:ss.fff")} - waiting complete");
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
            System.Diagnostics.Debug.WriteLine($"{DateTime.UtcNow.ToString("H:mm:ss.fff")} - iterations {iterations}");
            var isCompleted = IsTestComplete(EndTime(startTime, _rampUpSeconds, _holdForSeconds), iterations);
            System.Diagnostics.Debug.WriteLine($"{DateTime.UtcNow.ToString("H:mm:ss.fff")} - is test completed {isCompleted}");
            return isCompleted;
        }

        private int TotalAllowedRequestsToNow(int millisecondsEllapsed)
        {
            if (millisecondsEllapsed <= 0 || _throughput <= 0) { 
                return 0;
            }
            double totalRpsToNow;
            var secondsEllapsed = Convert.ToInt32(millisecondsEllapsed / 1000);
            System.Diagnostics.Debug.WriteLine($"{DateTime.UtcNow.ToString("H:mm:ss.fff")} - calculating tokens, seconds ellapsed {secondsEllapsed}");
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
            System.Diagnostics.Debug.WriteLine($"{DateTime.UtcNow.ToString("H:mm:ss.fff")} - total allowed requests to now {totalRpsToNow}");
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
                        System.Diagnostics.Debug.WriteLine($"{DateTime.UtcNow.ToString("H:mm:ss.fff")} - releasing {tokensToRelease}");
                        tokensReleased += tokensToRelease;
                        System.Diagnostics.Debug.WriteLine($"{DateTime.UtcNow.ToString("H:mm:ss.fff")} - total tokens released {tokensReleased}");
                        _taskExecution.Release(tokensToRelease);
                    }
                }
                await Task.Delay(TimeSpan.FromMilliseconds(100), ct); //todo configure
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
        Task ReleaseTokens(DateTime startTime, CancellationToken ct);
    }
}
