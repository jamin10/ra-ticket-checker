using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Raticketbot;

public class PollingHostedService : BackgroundService
{
    private readonly Poller _poller;
    private readonly Notifier _notifier;
    private readonly ILogger<PollingHostedService> _logger;
    private readonly int _intervalSeconds;

    private bool _lastKnownAvailable = false;

    public PollingHostedService(Poller poller, Notifier notifier, IConfiguration config, ILogger<PollingHostedService> logger)
    {
        _poller = poller;
        _notifier = notifier;
        _logger = logger;
        _intervalSeconds = config.GetValue<int?>("PollIntervalSeconds") ?? 60;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting PollingHostedService with interval {Interval}s", _intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var available = await _poller.CheckAsync(stoppingToken);

                if (available && !_lastKnownAvailable)
                {
                    _logger.LogInformation("Ticket availability detected — sending notification");
                    await _notifier.SendAsync($"Tickets appear available at {DateTime.UtcNow:o}", stoppingToken);
                    _lastKnownAvailable = true;
                }
                else if (!available && _lastKnownAvailable)
                {
                    _logger.LogInformation("Tickets are no longer available");
                    _lastKnownAvailable = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during polling loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
        }
    }
}
