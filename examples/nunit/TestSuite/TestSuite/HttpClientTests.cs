using NUnit.Framework;
using System.Threading.Tasks;

namespace TestSuite
{
    public class HttpClientTests
    {
        [Test]
        [Property("Name", "HttpExample")]
        public async Task SendHttpGet()
        {
            var response = await GlobalState.HttpClient.GetAsync("/");
            Assert.IsTrue(response.IsSuccessStatusCode);
            var content = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(content.Contains("<h1>Welcome to the Simple Travel Agency!</h1>"));
        }
    }
}
