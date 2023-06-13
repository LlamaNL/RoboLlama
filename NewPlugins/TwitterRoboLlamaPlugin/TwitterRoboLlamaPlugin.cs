using Microsoft.Playwright;
using RoboLlamaLibrary.Plugins;
using System.Text;

namespace TwitterRoboLlamaPlugin;

public class TwitterRoboLlamaPlugin : ITriggerWordPlugin, IPluginConfig
{
    private Dictionary<string, string?>? _config;

    public Dictionary<string, Func<string, IEnumerable<string>>> GetTriggerWords()
    {
        return new Dictionary<string, Func<string, IEnumerable<string>>>
        {
            ["twitter"] = (message) => GetResponse(message).GetAwaiter().GetResult(),
            ["nitter"] = (message) => GetResponse(message).GetAwaiter().GetResult()
        };
    }

    public void SetConfig(Dictionary<string, string?> config)
    {
        _config = config;
    }

    public async Task<IEnumerable<string>> GetResponse(string input)
    {
        try
        {
            using IPlaywright playwright = await Playwright.CreateAsync();
            await using IBrowser browser = await playwright.Chromium.LaunchAsync();

            // Navigate to the page
            string url = input; // Replace with the URL you want to scrape (ensure it is allowed)
            IPage page = await browser.NewPageAsync();
            await page.GotoAsync(url);

            // Wait for the specific element to be loaded
            await page.WaitForSelectorAsync("div[data-testid='tweetText']");

            // Select the first div with the specific data-testid attribute
            var firstDiv = await page.QuerySelectorAsync("div[data-testid='tweetText']");

            // Use JavaScript to extract content and URLs in order
            string result = await page.EvalOnSelectorAsync<string>("div[data-testid='tweetText']", @"(div) => {
                let content = '';
                let nodes = div.childNodes;
                for (let node of nodes) {
                    if (node.nodeName === 'SPAN') {
                        content += node.innerText + ' ';
                    } else if (node.nodeName === 'A') {
                        content += node.innerText + ' ';
                    }
                }
                return content.trim();
            }");

            await browser.CloseAsync();
            var output = "[Tweet] " + result.Replace("\n", "").Replace("\r", "").Trim();
            return new List<string>() { output };
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }
}
