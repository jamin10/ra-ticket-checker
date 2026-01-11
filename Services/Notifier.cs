using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Raticketbot;

/// <summary>
/// Notifier attempts to use the WhatsappBusiness.CloudApi wrapper if registered.
/// If the wrapper isn't available at runtime, it falls back to calling the WhatsApp Cloud API directly using HttpClient.
/// This avoids a hard compile-time dependency on wrapper types while still supporting it when present.
/// </summary>
public class Notifier
{
    private readonly IServiceProvider _services;
    private readonly ILogger<Notifier> _logger;
    private readonly IConfiguration _whatsappSettings;
    private readonly string _toPhoneNumber;
    private readonly string _accessToken;
    private readonly string _phoneNumberId;

    public Notifier(IServiceProvider services, IConfiguration config, ILogger<Notifier> logger)
    {
        _services = services;
        _logger = logger;
        _whatsappSettings = config.GetSection("WhatsAppBusinessCloudApiConfiguration");
        _toPhoneNumber = _whatsappSettings["ToPhoneNumber"];
        _accessToken = config.GetValue<string>("WhatsAppBusinessCloudApiConfiguration:AccessToken") ?? string.Empty;
        _phoneNumberId = config.GetValue<string>("WhatsAppBusinessCloudApiConfiguration:WhatsAppBusinessPhoneNumberId") ?? string.Empty;
    }

    public async Task SendAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_toPhoneNumber))
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

        using var http = new HttpClient();
        var url = $"https://graph.facebook.com/v24.0/{_phoneNumberId}/messages";

        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = _toPhoneNumber,
            type = "template",
            template = new { name = "hello_world",
                         language = new { code = "en_US" } }
        };

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

