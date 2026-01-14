using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Raticketbot;

public class Notifier
{
    private readonly IServiceProvider _services;
    private readonly ILogger<Notifier> _logger;
    private readonly IConfiguration _whatsappSettings;
    private readonly List<string> _toPhoneNumbers;
    private readonly string _targetUrl;
    private readonly string _accessToken;
    private readonly string _phoneNumberId;

    public Notifier(IServiceProvider services, IConfiguration config, ILogger<Notifier> logger)
    {
        _services = services;
        _logger = logger;
        _whatsappSettings = config.GetSection("WhatsAppBusinessCloudApiConfiguration");
        _toPhoneNumbers = _whatsappSettings.GetSection("ToPhoneNumbers").Get<List<string>>();
        _targetUrl = config["TargetUrl"];
        _accessToken = config.GetValue<string>("WhatsAppBusinessCloudApiConfiguration:AccessToken") ?? string.Empty;
        _phoneNumberId = config.GetValue<string>("WhatsAppBusinessCloudApiConfiguration:WhatsAppBusinessPhoneNumberId") ?? string.Empty;
    }

    public async Task SendAsync(CancellationToken ct = default)
    {
        if (_toPhoneNumbers == null || !_toPhoneNumbers.Any())
        {
            _logger.LogWarning("No destination phone number configured. Set WhatsAppBusinessCloudApiConfiguration:ToPhoneNumber in appsettings or env.");
            return;
        }

        await SendUsingHttpClientAsync(ct);
    }

    private async Task SendUsingHttpClientAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_accessToken) || string.IsNullOrWhiteSpace(_phoneNumberId))
        {
            _logger.LogWarning("Missing AccessToken or PhoneNumberId for direct API fallback. Set in configuration or env.");
            return;
        }

        foreach (var phoneNumber in _toPhoneNumbers)
        {
            using var http = new HttpClient();
            var url = $"https://graph.facebook.com/v24.0/{_phoneNumberId}/messages";

            var payload = new
            {
                messaging_product = "whatsapp",
                recipient_type = "individual",
                to = phoneNumber,
                type = "template",
                template = new
                {
                    name = "ticketavailable",
                    language = new { code = "en" }
                }
            };

            // type = "text",
            // text = new { body = $"Tickets are available! {_targetUrl}", preview_url = true }


            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            // Add required headers
            //request.Headers.Add("User-Agent", "ra-ticket-checker/1.0");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _accessToken);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            var resp = await http.SendAsync(request, ct);
            var respBody = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("Sent message via Graph API. Response: {Response}", respBody);
            }
            else
            {
                _logger.LogError("Graph API send failed: {Status} {Response}", resp.StatusCode, respBody);
            }
        }
    }
}

