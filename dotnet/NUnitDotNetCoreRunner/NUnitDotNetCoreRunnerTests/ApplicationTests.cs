using FluentAssertions;
using Moq;
using NUnitDotNetCoreRunner.Services;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NUnitDotNetCoreRunnerTests
{
    public class ApplicationTests
    {
        [Theory]
        [InlineAutoMoqData(1, 0, 0, 2, 1)]
        [InlineAutoMoqData(2, 0, 0, 2, 2)]
        [InlineAutoMoqData(4, 0, 0, 2, 20)]
        [InlineAutoMoqData(3, 0, 0, 2, 1)]
        [InlineAutoMoqData(1, 0, 0, 2, 2)]
        [InlineAutoMoqData(1, 1, 0, 2, 1)]
        [InlineAutoMoqData(2, 1, 0, 2, 2)]
        [InlineAutoMoqData(3, 1, 0, 2, 1)]
        [InlineAutoMoqData(1, 1, 0, 2, 2)]
        [InlineAutoMoqData(4, 0, 1, 2, 1)]
        [InlineAutoMoqData(4, 1, 1, 2, 2)]
        [InlineAutoMoqData(2, 0.9, 0, 6, 1)]

        public async Task WhenIterationsSpecified_ThenIterationsAreNotExceeded(
            int concurrency,
            double throughput,
            int rampUpSeconds,
            int holdForSeconds,
            int iterations,
            Mock<IReportWriter> reportWriter,
            Mock<INUnitAdapter> nUnitAdapter)
        {
            var cts = new CancellationTokenSource();
            var threadControl = new ThreadControl(throughput, iterations, rampUpSeconds, holdForSeconds);
            var threadAllocator = new ThreadAllocator(nUnitAdapter.Object, threadControl);
            var app = new Application(reportWriter.Object, threadAllocator, threadControl);

            cts.CancelAfter(TimeSpan.FromSeconds(holdForSeconds + rampUpSeconds + 1));
            await app.Run(concurrency, throughput, rampUpSeconds, holdForSeconds, cts.Token);
            nUnitAdapter.Verify(n =>
                n.RunTest(It.IsRegex("worker_.+")),
                Times.Exactly(iterations));
        }

        [Theory]
        [InlineAutoMoqData(1, 0, 0, 2)]
        [InlineAutoMoqData(2, 0, 2, 2)]
        [InlineAutoMoqData(3, 0, 2, 0)]
        [InlineAutoMoqData(2, 1, 0, 2)]
        [InlineAutoMoqData(3, 1, 2, 2)]
        [InlineAutoMoqData(4, 1, 2, 0)]
        public async Task WhenDurationSpecified_ThenDurationIsObserved(
            int concurrency,
            double throughput,
            int rampUpSeconds,
            int holdForSeconds,
            Mock<IReportWriter> reportWriter,
            Mock<INUnitAdapter> nUnitAdapter)
        {
            var cts = new CancellationTokenSource();
            var threadControl = new ThreadControl(throughput, iterations: 0, rampUpSeconds, holdForSeconds);
            var threadAllocator = new ThreadAllocator(nUnitAdapter.Object, threadControl);
            var app = new Application(reportWriter.Object, threadAllocator, threadControl);

            var watch = new Stopwatch();
            watch.Start(); 
            cts.CancelAfter(TimeSpan.FromSeconds(holdForSeconds + rampUpSeconds + 1));
            await app.Run(concurrency, throughput, rampUpSeconds, holdForSeconds, cts.Token);
            watch.Stop();
            watch.Elapsed.Should().BeGreaterOrEqualTo(
                TimeSpan.FromSeconds(holdForSeconds + rampUpSeconds)
                        .Subtract(TimeSpan.FromMilliseconds(20)));
        }

        [Theory]
        [InlineAutoMoqData(3, 1, 2, 3, 25)]
        [InlineAutoMoqData(1, 0, 2, 2, 1000)]
        [InlineAutoMoqData(3, 1, 0, 3, 25)]
        [InlineAutoMoqData(1, 0, 0, 2, 1000)]
        public async Task WhenMoreIterationsThanDurationAllows_ThenTestExitsEarly(
            int concurrency,
            double throughput,
            int rampUpSeconds,
            int holdForSeconds,
            int iterations,
            Mock<IReportWriter> reportWriter)
        {
            var cts = new CancellationTokenSource();
            var nUnit = new NUnitAdapterFake();
            var threadControl = new ThreadControl(throughput, iterations, rampUpSeconds, holdForSeconds);
            var threadAllocator = new ThreadAllocator(nUnit, threadControl);
            var app = new Application(reportWriter.Object, threadAllocator, threadControl);

            cts.CancelAfter(TimeSpan.FromSeconds(holdForSeconds + rampUpSeconds + 1));
            await app.Run(concurrency, throughput, rampUpSeconds, holdForSeconds, cts.Token);

            nUnit.Calls.Should().BeLessThan(iterations);
        }

        [Theory]
        [InlineAutoMoqData(1, 1, 0, 4)]
        [InlineAutoMoqData(2, 1, 0, 4)]
        [InlineAutoMoqData(1, 0.8, 0, 4)]
        [InlineAutoMoqData(2, 2, 0, 3)]
        [InlineAutoMoqData(2, 20, 0, 4)]
        [InlineAutoMoqData(1, 1, 2, 5)]
        [InlineAutoMoqData(2, 1, 2, 4)]
        [InlineAutoMoqData(1, 0.8, 2, 5)]
        [InlineAutoMoqData(2, 2, 2, 2)]
        public async Task WhenThroughputIsSpecified_ThenRPSIsNotExceeded(
            int concurrency,
            double throughput,
            int rampUpSeconds,
            int holdForSeconds,
            Mock<IReportWriter> reportWriter,
            Mock<INUnitAdapter> nUnitAdapter)
        {
            var cts = new CancellationTokenSource();
            var threadControl = new ThreadControl(throughput, iterations: 0, rampUpSeconds, holdForSeconds);
            var threadAllocator = new ThreadAllocator(nUnitAdapter.Object, threadControl);
            var app = new Application(reportWriter.Object, threadAllocator, threadControl);

            cts.CancelAfter(TimeSpan.FromSeconds(holdForSeconds + rampUpSeconds + 1));
            await app.Run(concurrency, throughput, rampUpSeconds, holdForSeconds, cts.Token);

            var expectedTotal = throughput * holdForSeconds +
                (rampUpSeconds * throughput / 2);
            var tps = concurrency * throughput;

            nUnitAdapter.Verify(n =>
                n.RunTest(It.IsRegex("worker_.+")),
                Times.Between(Convert.ToInt32(expectedTotal - tps), Convert.ToInt32(expectedTotal), Moq.Range.Inclusive));
        }

        class NUnitAdapterFake : INUnitAdapter
        {
            public int Calls;
            
            public void RunTest(string threadName)
            {
                Interlocked.Increment(ref Calls);
                Thread.Sleep(400);
            }
        }
    }
}
