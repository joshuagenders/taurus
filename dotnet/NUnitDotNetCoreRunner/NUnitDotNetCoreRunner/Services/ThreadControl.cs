using NUnitDotNetCoreRunner.Models;
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

        private int _actualIterations;
        private DateTime _startTime { get; set; }
        private DateTime _lastTokenRequest { get; set; }

        public ThreadControl(double throughput, int iterations, int rampUpSeconds, int holdForSeconds)
        {
            _taskExecution = new SemaphoreSlim(0);
            _throughput = throughput;
            _iterations = iterations;
            _rampUpSeconds = rampUpSeconds;
            _holdForSeconds = holdForSeconds;
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
            //todo locking
            var actualIterations = Interlocked.Increment(ref _actualIterations);
            if (actualIterations > _iterations)
            {
                throw new IterationsExceededException($"Iterations exceeded. Iterations: {_iterations}. Actual: ${actualIterations}");
            }
        }

        public async Task ReleaseTokens(CancellationToken ct)
        {
            var accumulatedTokens = 0d;
            var total = 0;
            while (!IsTestComplete() && !ct.IsCancellationRequested)
            {
                accumulatedTokens += _throughput * PercentageThroughRampUp;
                var tokensToRelease = (int)accumulatedTokens;
                if (_iterations > 0)
                {
                    if (tokensToRelease + total > _iterations)
                    {
                        tokensToRelease = _iterations - total;
                    }
                    if (int.MaxValue - tokensToRelease < total)
                    {
                        total += (int)accumulatedTokens;
                    }
                    else
                    {
                        total = int.MaxValue;
                    }
                }
                accumulatedTokens = Math.Truncate(accumulatedTokens);
                _taskExecution.Release(tokensToRelease);
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }
 
        private double SecondsFromStart => _startTime.Subtract(DateTime.UtcNow).TotalSeconds;

        private double PercentageThroughRampUp => SecondsFromStart > _rampUpSeconds
            ? 1
            : SecondsFromStart / _rampUpSeconds;

        public bool IsTestComplete() =>
            (_actualIterations > 0 && _iterations >= _actualIterations)
            || DateTime.UtcNow > EndTime(_rampUpSeconds, _holdForSeconds);

        private DateTime EndTime(int rampUpSeconds, int holdForSeconds) =>
            _startTime.AddSeconds(rampUpSeconds + holdForSeconds);
    }

    public interface IThreadControl
    {
        Task RequestTaskExecution(CancellationToken ct);
        int ReleaseTaskExecution(int count = 1);
        bool IsTestComplete();
        Task ReleaseTokens(CancellationToken ct);
    }
}
