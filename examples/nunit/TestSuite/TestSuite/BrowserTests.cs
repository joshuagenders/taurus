using NUnit.Framework;
using System.Threading.Tasks;

namespace TestSuite
{
    [SingleThreaded]
    public class BrowserTests
    {
        [Test]
        [Property("Name", "BrowserExample")]
        public async Task BrowserExample()
        {
            using (var page = await GlobalState.Browser.NewPageAsync())
            {
                await page.GoToAsync(Config.BaseUrl);
                await page.WaitForSelectorAsync("div.container");
                var title = await page.EvaluateExpressionAsync<dynamic>("document.getElementsByTagName('h1')[0].innerText");
                Assert.IsTrue(title == "Welcome to the Simple Travel Agency!");
            }
        }
    }
}