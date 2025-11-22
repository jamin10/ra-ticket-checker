# raticketbot

Small .NET console app that polls a given event page and sends a WhatsApp message (via Meta WhatsApp Business Cloud API) when tickets appear available.

Quick start (macOS zsh):

1. Configure environment or edit `appsettings.json` with your WhatsApp Business Cloud API values. You can also export environment variables:

```bash
export WhatsAppBusinessCloudApiConfiguration__AccessToken="YOUR_ACCESS_TOKEN"
export WhatsAppBusinessCloudApiConfiguration__WhatsAppBusinessPhoneNumberId="YOUR_PHONE_NUMBER_ID"
export WhatsAppBusinessCloudApiConfiguration__ToPhoneNumber="+1234567890"
```

2. Restore and run:

```bash
cd /Users/benwhitfield/workspace/raticketbot
dotnet restore
dotnet build
dotnet run --project .
```

Notes:
- This project uses the `WhatsappBusiness.CloudApi` NuGet package as a thin wrapper over the WhatsApp Cloud API.
- The detection logic is a simple keyword-based heuristic (configured in `appsettings.json`). Consider improving parsing with an HTML parser or by targeting specific page elements.
- Sending real WhatsApp messages requires a Meta App with WhatsApp Business API access and a phone number ID; follow Meta's docs to obtain a temporary access token for testing.
