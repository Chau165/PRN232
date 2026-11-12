# payOS B4: SDK and server configuration

Infrastructure references the official `payOS` NuGet package, pinned at 2.1.0.
API registers PayOSClient using explicitly bound PayOSSettings. Credentials never
go into frontend VITE variables or API response DTOs. The shared admin channel is
configured once for all properties; Managers do not supply their own API keys.

Official references:
- https://payos.vn/docs/sdks/back-end/net/
- https://www.nuget.org/packages/payOS/2.1.0

## Local setup

From PRN232_BE/PRN232.Api, set values in User Secrets (replace placeholders locally,
do not paste real keys into chat or commit them):

```powershell
dotnet user-secrets set "PayOS:ClientId" "<client-id>"
dotnet user-secrets set "PayOS:ApiKey" "<api-key>"
dotnet user-secrets set "PayOS:ChecksumKey" "<checksum-key>"
dotnet user-secrets set "PayOS:ReturnUrl" "http://localhost:8443/my-bills?payment=return"
dotnet user-secrets set "PayOS:CancelUrl" "http://localhost:8443/my-bills?payment=cancel"
dotnet user-secrets set "PayOS:Enabled" "true"
```

User Secrets are loaded in Development. Use the FE's actual origin/port if different.
The two URLs are browser destinations, not webhook endpoints. Returning to either
URL is not proof of payment. Frontend status handling is a later step.

## Deployment

Configure environment variables on the backend service:

```text
PayOS__Enabled=true
PayOS__ClientId=<client-id>
PayOS__ApiKey=<api-key>
PayOS__ChecksumKey=<checksum-key>
PayOS__ReturnUrl=https://your-frontend.example/my-bills?payment=return
PayOS__CancelUrl=https://your-frontend.example/my-bills?payment=cancel
```

The application reads these .NET configuration names, not the SDK's optional
PAYOS_CLIENT_ID defaults. No secrets are provided in appsettings.json.

Disabled (the default): credentials/URLs are not required and no SDK client is
registered. Enabled: missing/placeholder credentials or invalid URLs fail startup
with configuration key names only, without printing values. URLs must be absolute
HTTPS, without user credentials/fragments. HTTP loopback is allowed only in
Development/Testing. This validates configuration shape, not whether payOS accepts
the account's credentials. Never log the settings object or SDK secret properties.

## Scope of B4

Constructing the SDK client performs no payment request and registers no webhook.
No real account keys, DB changes, checkout links or money movements are made by
this step. Enabling the settings does not switch the existing IPaymentGateway:
the invoice/booking pay endpoints are still simulated until their later workflow
is implemented and the old invoice path is disabled. Creating payment links,
signature verification, callbacks, reconciliation and FE integration remain later
steps; this configuration alone is not a production payment integration.
