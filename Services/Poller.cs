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
        await page.GotoAsync(_targetUrl);

        await page.WaitForTimeoutAsync(RandomMs(800, 1500));
        await page.Mouse.MoveAsync(300, 400, new() { Steps = 20 });

        var frameLocator = page.FrameLocator("iframe[data-testid='tickets-iframe-2325782']");

        var tierFinal = frameLocator.Locator("li:has-text(\"Final Release (Anytime Entry) - £40.00 + BF\")").Last;
        var className = await tierFinal.GetAttributeAsync("class");

        if (className != "closed")
        {
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
