using AutoFixture.Xunit2;
using Moq;
using NUnitDotNetCoreRunner.Services;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NUnitDotNetCoreRunnerTests
{
    public class ThreadAllocatorTests
    {
        [Theory]
        [InlineAutoMoqData(1, 0, 0, 0, 1)]
        public async Task WhenThreadAllocatorExecutes(
            int concurrency, 
            double throughput, 
            int rampUpMinutes, 
            int holdForMinutes, 
            int iterations,
            ThreadControl threadControl,
            Mock<IReportWriter> reportWriter,
            Mock<INUnitAdapter> nUnitAdapter)
        {
            var threadAllocator = new ThreadAllocator(reportWriter.Object, threadControl, nUnitAdapter.Object);
            await threadAllocator.Run(concurrency, throughput, rampUpMinutes, holdForMinutes, iterations);
            nUnitAdapter.Verify(n => n.RunTest(It.IsRegex("worker_.+"), It.IsAny<CancellationToken>()), Times.Exactly(iterations));
            /*
             * todo test cases
             when tasks are cancelled
                all tasks exit as iteration finishes
             when no ramp up
                threads all queue instantly
                duration is same as hold for
                threads complete near the same time
             when iterations
                then limit applied
                applied during rampup
             when more iterations than duration allows
                then test exits before iterations reached
             when throughput 1, <1, >1
                then ramp-up is correctly applied
                lots of threads and low RPS doesn't exceed RPS
                lots of threads, high RPS and a ramp up doesn't exceed RPS early in test and both threads and ramp up are limited
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
}
