using NUnit.Framework;
using PuppeteerSharp;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TestSuite
{
    [SetUpFixture]

    public class GlobalState
    {
        public static Browser Browser { get; private set; }
        public static HttpClient HttpClient { get; set; }
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);

        [OneTimeSetUp]
        public async Task Init() 
        { 
            try
            {
                await _semaphore.WaitAsync();
                await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);
                if (Browser == null)
                {
                    Browser = await Puppeteer.LaunchAsync(new LaunchOptions
                    {
                        Headless = true
                    });
                }
                if (HttpClient == null)
                {
                    HttpClient = new HttpClient();
                    HttpClient.BaseAddress = new Uri(Config.BaseUrl);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        [OneTimeTearDown]
        public async Task TearDown()
        {
            await Browser?.CloseAsync();
            HttpClient?.Dispose();
        }
    }
}
