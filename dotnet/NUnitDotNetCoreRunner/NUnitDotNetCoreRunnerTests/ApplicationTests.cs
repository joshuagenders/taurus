using FluentAssertions;
using Moq;
using NUnitDotNetCoreRunner.Services;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NUnitDotNetCoreRunnerTests
{
    public class ApplicationTests
    {
        [Theory]
        // [InlineAutoMoqData(1, 0, 0, 2, 1)]
        // [InlineAutoMoqData(2, 0, 0, 2, 2)]
        // [InlineAutoMoqData(4, 0, 0, 2, 20)]
        // [InlineAutoMoqData(3, 0, 0, 2, 1)]
        // [InlineAutoMoqData(1, 0, 0, 2, 2)]
        // [InlineAutoMoqData(1, 1, 0, 2, 1)]
        // [InlineAutoMoqData(2, 1, 0, 2, 2)]
        // [InlineAutoMoqData(3, 1, 0, 2, 1)]
        // [InlineAutoMoqData(1, 1, 0, 2, 2)]
        // [InlineAutoMoqData(4, 0, 1, 2, 1)]
        // [InlineAutoMoqData(4, 1, 1, 2, 2)]
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
            var threadControl = new ThreadControl(throughput, iterations, rampUpSeconds, holdForSeconds);
            var threadAllocator = new ThreadAllocator(nUnitAdapter.Object, threadControl);
            var app = new Application(reportWriter.Object, threadAllocator, threadControl);

            await app.Run(concurrency, throughput, rampUpSeconds, holdForSeconds);
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
            var threadControl = new ThreadControl(throughput, iterations: 0, rampUpSeconds, holdForSeconds);
            var threadAllocator = new ThreadAllocator(nUnitAdapter.Object, threadControl);
            var app = new Application(reportWriter.Object, threadAllocator, threadControl);

            var watch = new Stopwatch();
            watch.Start();
            await app.Run(concurrency, throughput, rampUpSeconds, holdForSeconds);
            watch.Stop();
            watch.Elapsed.Should().BeGreaterOrEqualTo(TimeSpan.FromSeconds(holdForSeconds + rampUpSeconds));
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
            var nUnit = new NUnitAdapterFake();
            var threadControl = new ThreadControl(throughput, iterations, rampUpSeconds, holdForSeconds);
            var threadAllocator = new ThreadAllocator(nUnit, threadControl);
            var app = new Application(reportWriter.Object, threadAllocator, threadControl);

            await app.Run(concurrency, throughput, rampUpSeconds, holdForSeconds);

            nUnit.Calls.Should().BeLessThan(iterations);
        }

        [Theory]
        [InlineAutoMoqData(1, 1, 0, 3)]
        [InlineAutoMoqData(2, 1, 0, 3)]
        [InlineAutoMoqData(1, 0.8, 0, 3)]
        [InlineAutoMoqData(2, 2, 0, 3)]
        [InlineAutoMoqData(2, 20, 0, 3)]
        [InlineAutoMoqData(1, 1, 2, 4)]
        [InlineAutoMoqData(2, 1, 2, 4)]
        [InlineAutoMoqData(1, 0.8, 2, 4)]
        [InlineAutoMoqData(2, 2, 2, 4)]
        [InlineAutoMoqData(2, 20, 2, 4)]
        public async Task WhenThroughputIsSpecified_ThenRPSIsNotExceeded(
            int concurrency,
            double throughput,
            int rampUpSeconds,
            int holdForSeconds,
            Mock<IReportWriter> reportWriter,
            Mock<INUnitAdapter> nUnitAdapter)
        {
            var threadControl = new ThreadControl(throughput, iterations: 0, rampUpSeconds, holdForSeconds);
            var threadAllocator = new ThreadAllocator(nUnitAdapter.Object, threadControl);
            var app = new Application(reportWriter.Object, threadAllocator, threadControl);

            await app.Run(concurrency, throughput, rampUpSeconds, holdForSeconds);

            var expectedTotal = throughput * concurrency * holdForSeconds +
                Enumerable.Range(0, rampUpSeconds)
                    .Select(s => s * concurrency * throughput)
                    .Sum();

            nUnitAdapter.Verify(n =>
                n.RunTest(It.IsAny<string>()),
                Times.Between(Convert.ToInt32(expectedTotal - 1), Convert.ToInt32(expectedTotal + 1), Moq.Range.Inclusive));
        }

        class NUnitAdapterFake : INUnitAdapter
        {
            public int Calls;
            
            public void RunTest(string threadName)
            {
                Interlocked.Increment(ref Calls);
                Thread.SpinWait(300);
            }
        }
        /*
             * todo test cases
            
             when throughput 1, <1, >1
                then ramp-up is correctly applied
                lots of threads and low RPS doesn't exceed RPS
                lots of threads, high RPS and a ramp up doesn't exceed RPS early 
                    in test and both threads and ramp up are limited
             when more threads than iterations in an interval
                then iterations aren't exceeded
             when more throughput than iterations in an interval 
                e.g. 20 RPS, 3 threads, 1 iteration
                e.g. 0.5 RPS, 5 threads, 1 iteration
                then iterations are not exceeded
            test duration is not exceeded
             */

    }
}
