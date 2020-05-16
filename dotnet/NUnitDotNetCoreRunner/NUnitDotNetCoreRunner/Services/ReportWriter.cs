using Newtonsoft.Json;
using NUnitRunner.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace NUnitRunner.Services
{
    public class ReportWriter : IReportWriter
    {
        private readonly ConcurrentQueue<ReportItem> _reportItems;
        private readonly string _reportFile;

        public ReportWriter(ConcurrentQueue<ReportItem> reportItems, string reportFile)
        {
            _reportItems = reportItems;
            _reportFile = reportFile;
        }

        public bool TestsCompleted { get; set; }

        public async Task StartWriting()
        {
            using (var streamWriter = new StreamWriter(_reportFile))
            {
                streamWriter.AutoFlush = true;

                while (!TestsCompleted || _reportItems.Count > 0)
                {
                    if (_reportItems.TryDequeue(out var item))
                    {
                        var report = GetReport(item);
                        await streamWriter.WriteLineAsync(report);
                    }
                }
            }
        }

        private string GetReport(ReportItem item)
        {
            var sample = new Dictionary<object, object>
            {
                { "start_time", item.StartTime },
                { "workerID", item.ThreadName },
                { "duration", item.Duration },
                { "test_case", item.TestCase },
                { "test_suite", item.TestSuite },
                { "status", item.Status },
                { "error_msg", item.ErrorMessage },
                { "error_trace", item.ErrorTrace },
                { "extras", item.Extras }
            };

            return JsonConvert.SerializeObject(sample);
        }
    }

    public interface IReportWriter
    {
        Task StartWriting();
        bool TestsCompleted { get; set; }
    }
}
