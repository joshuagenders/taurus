using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NUnitDotNetCoreRunner.Services
{
    public class Application : IApplication
    {
        private readonly IReportWriter _reportWriter;
        private readonly IThreadControl _threadControl;
        private readonly IThreadAllocator _threadAllocator;
        private readonly SemaphoreSlim _executionSemaphore;

        public Application(
            IReportWriter reportWriter,
            IThreadAllocator threadAllocator,
            IThreadControl threadControl)
        {
            _reportWriter = reportWriter;
            _threadControl = threadControl;
            _threadAllocator = threadAllocator;
            _executionSemaphore = new SemaphoreSlim(1);
        }

        public async Task Run(
            int concurrency,
            double throughput,
            int rampUpSeconds,
            int holdForSeconds,
            CancellationToken ct)
        {
            await _executionSemaphore.WaitAsync(ct);
            try
            {
                var startTime = DateTime.UtcNow;
                var testDuration = TestDuration(rampUpSeconds, holdForSeconds);
                var reportWriterTask = Task.Run(() => _reportWriter.StartWriting(ct), ct);
                var threadCreationTask = Task.Run(() =>_threadAllocator.StartThreads(startTime, concurrency, rampUpSeconds, ct), ct);
                var testDurationTask = throughput > 0
                    ? Task.Run(() => _threadControl.ReleaseTokens(startTime, ct), ct)
                    : Task.Run(() => Task.Delay(testDuration, ct), ct);

                await threadCreationTask;
                await testDurationTask;
                _reportWriter.TestsCompleted = true;
                await reportWriterTask;
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
            catch (AggregateException e) when (e.InnerExceptions.All(x => x is TaskCanceledException || x is OperationCanceledException)) { }
            finally
            {
                _executionSemaphore.Release();
            }
        }

        private TimeSpan TestDuration (int rampUpSeconds, int holdForSeconds) => 
            TimeSpan.FromSeconds(rampUpSeconds + holdForSeconds);
    }

    public interface IApplication
    {
        Task Run(
            int concurrency,
            double throughput,
            int rampUpSeconds,
            int holdForSeconds,
            CancellationToken ct);
    }
}
