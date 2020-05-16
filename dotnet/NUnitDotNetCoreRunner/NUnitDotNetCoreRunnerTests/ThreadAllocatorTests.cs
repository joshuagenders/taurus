using System;
using Xunit;

namespace NUnitDotNetCoreRunnerTests
{
    public class UnitTest1
    {
        [Theory]
        [InlineAutoMoqData]
        public void WhenThreadAllocatorExecutes(
            int concurrency, 
            double throughput, 
            int rampUpMinutes, 
            int holdForMinutes, 
            int iterations)
        {
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
