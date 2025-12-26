using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Raticketbot;

public class Poller
{
    private readonly HttpClient _http;
    private readonly ILogger<Poller> _logger;
    private readonly string _targetUrl;
    private readonly string[] _positiveKeywords;
    private readonly string[] _negativeKeywords;

    public Poller(HttpClient http, IConfiguration config, ILogger<Poller> logger)
    {
    _http = http;
    _logger = logger;
    _targetUrl = config["TargetUrl"];

    _positiveKeywords = config.GetSection("Detection:PositiveKeywords").Get<string[]>();
    _negativeKeywords = config.GetSection("Detection:NegativeKeywords").Get<string[]>() ?? new[] { "Sold out", "sold out", "No tickets", "sold-out" };
    }

    public async Task<bool> CheckAsync(CancellationToken ct = default)
    {
        using var playwright = await Playwright.CreateAsync();

        var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions
            {
                Headless = false,
                Args = new[]
                {
                    "--disable-blink-features=AutomationControlled",
                    "--disable-infobars",
                    "--start-maximized"
                }
                // Uncomment if using a proxy
                /*
                Proxy = new Proxy
                {
                    Server = "http://ip:port",
                    Username = "user",
                    Password = "pass"
                }
                */
            });

        var context = await browser.NewContextAsync(
            new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize
                {
                    Width = 1366,
                    Height = 768
                },
                Locale = "en-GB",
                UserAgent =
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36",
            });

        await ApplyStealthAsync(context);

        var page = await context.NewPageAsync();

        await page.GotoAsync(_targetUrl,
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        // Example human-like interaction
        await page.WaitForTimeoutAsync(RandomMs(800, 1500));
        await page.Mouse.MoveAsync(300, 400, new() { Steps = 20 });

        Console.WriteLine("Page loaded. Press ENTER to exit.");
        Console.ReadLine();

        // Count available ticket <li> elements (not closed)
        var magCount = await page.Locator(":nth-match(span:has-text(\"Magazine\"), 3)").CountAsync();
        //var text = await page.GetByText("Magazine").TextContentAsync();
        var tiers = page.Locator(":nth-match(div:has-text(\"Tier\"), 3)");
        //.Locator("xpath=../..");
        var className = await tiers.GetAttributeAsync("class");
        //var parent = await page.Locator("div:")
        var child = await page.GetByText("Anytime release 2 + re-entry (£30 + BF)").CountAsync();
        //var openTickets = await page.Locator("ul[data-ticket-info-selector-id='tickets-info'] > li:not(.closed)").CountAsync();
        await page.Locator("ul[data-ticket-info-selector-id='tickets-info'] > li").WaitForAsync(new() { State = WaitForSelectorState.Attached });
        var openTickets = await page.Locator("ul[data-ticket-info-selector-id='tickets-info'] > li").CountAsync();
        var count = await page.Locator("ol.collection-grid > li").CountAsync();
        
        var availableTickets = 0;
        if (availableTickets > 0)
        {
            _logger.LogInformation("Found {Count} open ticket(s).", availableTickets);
            return true;
        }

        return false;
    }

    static int RandomMs(int min, int max)
        => Random.Shared.Next(min, max);

    static async Task ApplyStealthAsync(IBrowserContext context)
    {
        await context.AddInitScriptAsync(@"
            // Remove webdriver flag
            Object.defineProperty(navigator, 'webdriver', {
                get: () => undefined
            });

            // Fake plugins
            Object.defineProperty(navigator, 'plugins', {
                get: () => [1, 2, 3, 4, 5],
            });

            // Fake languages
            Object.defineProperty(navigator, 'languages', {
                get: () => ['en-US', 'en'],
            });

            // Hardware
            Object.defineProperty(navigator, 'hardwareConcurrency', {
                get: () => 8,
            });

            // Chrome runtime
            window.chrome = {
                runtime: {},
            };

            // Permissions API
            const originalQuery = window.navigator.permissions.query;
            window.navigator.permissions.query = (parameters) =>
                parameters.name === 'notifications'
                    ? Promise.resolve({ state: Notification.permission })
                    : originalQuery(parameters);

            // WebGL spoofing
            const getParameter = WebGLRenderingContext.prototype.getParameter;
            WebGLRenderingContext.prototype.getParameter = function(parameter) {
                if (parameter === 37445) return 'Intel Inc.';
                if (parameter === 37446) return 'Intel Iris OpenGL Engine';
                return getParameter.call(this, parameter);
            };

            // Canvas fingerprint noise (light)
            const toDataURL = HTMLCanvasElement.prototype.toDataURL;
            HTMLCanvasElement.prototype.toDataURL = function() {
                const ctx = this.getContext('2d');
                ctx.globalAlpha = 0.99;
                return toDataURL.apply(this, arguments);
            };
        ");
    }
}
