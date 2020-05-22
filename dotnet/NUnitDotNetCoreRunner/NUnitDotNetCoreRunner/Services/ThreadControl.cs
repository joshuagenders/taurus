﻿using System;
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

        public async Task<bool> RequestTaskExecution(CancellationToken ct)
        {
            if (_enabled) await _taskExecution.WaitAsync(ct);
            return !ct.IsCancellationRequested;
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

        public async Task ReleaseTokens(CancellationToken ct)
        {
            var tokensReleased = 0;
            DateTime startTime = DateTime.UtcNow;

            while (!IsTestComplete(startTime))
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
                    tokensReleased += tokensToRelease;
                    _taskExecution.Release(tokensToRelease);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), ct);
            }
        }

        private bool IsTestComplete(DateTime startTime) =>
            (_iterations > 0 && _executionRequestCount >= _iterations)
            || DateTime.UtcNow > EndTime(startTime, _rampUpSeconds, _holdForSeconds);

        private DateTime EndTime(DateTime startTime, int rampUpSeconds, int holdForSeconds) =>
            startTime.AddSeconds(rampUpSeconds + holdForSeconds);
    }

    public interface IThreadControl
    {
        Task<bool> RequestTaskExecution(CancellationToken ct);
        int ReleaseTaskExecution(int count = 1);
        Task ReleaseTokens(CancellationToken ct);
    }
}
