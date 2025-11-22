using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly string _toPhoneNumber;
    private readonly string _accessToken;
    private readonly string _phoneNumberId;

    public Notifier(IServiceProvider services, IConfiguration config, ILogger<Notifier> logger)
    {
        _services = services;
        _logger = logger;
        _toPhoneNumber = config.GetValue<string>("WhatsAppBusinessCloudApiConfiguration:ToPhoneNumber") ?? string.Empty;
        _accessToken = config.GetValue<string>("WhatsAppBusinessCloudApiConfiguration:AccessToken") ?? string.Empty;
        _phoneNumberId = config.GetValue<string>("WhatsAppBusinessCloudApiConfiguration:WhatsAppBusinessPhoneNumberId") ?? string.Empty;
    }

    public async Task SendAsync(string message, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_toPhoneNumber))
        {
            _logger.LogWarning("No destination phone number configured. Set WhatsAppBusinessCloudApiConfiguration:ToPhoneNumber in appsettings or env.");
            return;
        }

        // Try to find a registered wrapper client via reflection
        var wrapperClient = ResolveWrapperClient();
        if (wrapperClient != null)
        {
            try
            {
                await SendUsingWrapperAsync(wrapperClient, message, ct);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Wrapper client failed — falling back to direct API call");
            }
        }

        // Fallback: send direct to Meta Graph API
        await SendUsingHttpClientAsync(message, ct);
    }

    private object? ResolveWrapperClient()
    {
        // Look for a service registered whose type name contains "WhatsAppBusinessClient" or "IWhatsAppBusinessClient"
        foreach (var service in new[] { "IWhatsAppBusinessClient", "WhatsAppBusinessClient" })
        {
            var type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypesSafe())
                .FirstOrDefault(t => string.Equals(t.Name, service, StringComparison.OrdinalIgnoreCase));

            if (type != null)
            {
                try
                {
                    var obj = _services.GetService(type);
                    if (obj != null) return obj;
                }
                catch { /* ignore */ }
            }
        }

        return null;
    }

    private async Task SendUsingWrapperAsync(object client, string message, CancellationToken ct)
    {
        // Try to create a TextMessageRequest via reflection and call SendTextMessageAsync
        var clientType = client.GetType();

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var textRequestType = assemblies.SelectMany(a => a.GetTypesSafe()).FirstOrDefault(t => t.Name == "TextMessageRequest");
        var whatsAppTextType = assemblies.SelectMany(a => a.GetTypesSafe()).FirstOrDefault(t => t.Name == "WhatsAppText");

        if (textRequestType == null || whatsAppTextType == null)
            throw new InvalidOperationException("Wrapper request types not found in assemblies");

        var textRequest = Activator.CreateInstance(textRequestType)!;
        var textObj = Activator.CreateInstance(whatsAppTextType)!;

        // set properties via reflection
        textRequestType.GetProperty("To")?.SetValue(textRequest, _toPhoneNumber);
        whatsAppTextType.GetProperty("Body")?.SetValue(textObj, message);
        whatsAppTextType.GetProperty("PreviewUrl")?.SetValue(textObj, false);
        textRequestType.GetProperty("Text")?.SetValue(textRequest, textObj);

        // find SendTextMessageAsync method
        var method = clientType.GetMethods().FirstOrDefault(m => m.Name == "SendTextMessageAsync");
        if (method == null) throw new InvalidOperationException("SendTextMessageAsync not found on wrapper client");

        var task = (Task?)method.Invoke(client, new[] { textRequest, ct });
        if (task == null) throw new InvalidOperationException("Wrapper SendTextMessageAsync returned null");

        await task.ConfigureAwait(false);
        _logger.LogInformation("Message sent via wrapper client");
    }

    private async Task SendUsingHttpClientAsync(string message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_accessToken) || string.IsNullOrWhiteSpace(_phoneNumberId))
        {
            _logger.LogWarning("Missing AccessToken or PhoneNumberId for direct API fallback. Set in configuration or env.");
            return;
        }

        using var http = new HttpClient();
        var url = $"https://graph.facebook.com/v17.0/{_phoneNumberId}/messages?access_token={_accessToken}";

        var payload = new
        {
            messaging_product = "whatsapp",
            to = _toPhoneNumber,
            type = "text",
            text = new { body = message }
        };

        var json = JsonSerializer.Serialize(payload);
        var resp = await http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"), ct);
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

internal static class ReflectionExtensions
{
    public static IEnumerable<Type> GetTypesSafe(this Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch
        {
            return Array.Empty<Type>();
        }
    }
}

