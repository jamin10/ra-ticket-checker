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
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = false });

        var page = await browser.NewPageAsync(new BrowserNewPageOptions
        {
            ViewportSize = new ViewportSize { Width = 1280, Height = 720 },
            UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 13_5_0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/118.0.5993.90 Safari/537.36"
        });

        await page.GotoAsync(_targetUrl);

        // Count available ticket <li> elements (not closed)
        int availableTickets = await page.EvaluateAsync<int>(
            @"() => document.querySelectorAll('ul[data-ticket-info-selector-id=""tickets-info""] > li:not(.closed)').length"
        );
        if (availableTickets > 0)
        {
            _logger.LogInformation("Found {Count} open ticket(s).", availableTickets);
            return true;
        }

        return false;
    }
}
