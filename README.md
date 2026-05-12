# Roggemans.GoogleAnalytics.Services

Proof-of-concept Google Analytics service and API for DiVintage.

The API exposes:

- `GET /health`
- `GET /config`
- `GET /api/google-analytics/divintage/summary`
- `POST /api/google-analytics/measurement-protocol/track`
- `POST /api/google-analytics/measurement-protocol/validate`

The MCP project exposes:

- `GET /mcp` for SSE transport
- `POST /mcp` for JSON-RPC tool calls
- GA4 API tools for configuration status, DiVintage summary reports, Measurement Protocol validation, page views, user identification, funnel steps, products, carts, checkout, purchases, forms, errors, custom events, and debug validation.
- The MCP host consumes the API through `Roggemans.GoogleAnalytics.ApiClient`; it should not call `Roggemans.GoogleAnalytics.Services` directly.

## Runtime Configuration

The repository does not store Google Analytics secrets. Configure them with environment variables or GitHub Actions secrets:

| Setting | Purpose |
| --- | --- |
| `GoogleAnalytics__MeasurementId` | GA4 measurement id, defaulted to DiVintage `G-XJWB055WRG`. |
| `GoogleAnalytics__MeasurementProtocolApiSecret` | GA4 Measurement Protocol API secret. |
| `GoogleAnalytics__PropertyId` | GA4 property id for Data API reports. |
| `GoogleAnalytics__ServiceAccountJsonBase64` | Base64-encoded Google service-account JSON with Analytics read access. |
| `GoogleAnalytics__OAuthClientId` | OAuth client id for a Google user account that has GA4 access. |
| `GoogleAnalytics__OAuthClientSecret` | OAuth client secret for refresh-token based report access. |
| `GoogleAnalytics__OAuthRefreshToken` | OAuth refresh token for durable user-account report access. |
| `GoogleAnalytics__AccessToken` | Optional OAuth access token alternative for short-lived tests. |
| `Mcp__ApiKey` | API key required by the MCP host in the `X-Mcp-ApiKey` header. |
| `GoogleAnalyticsApi__BaseUrl` | Base URL used by the MCP host to call `Roggemans.GoogleAnalytics.API`. |
| `GoogleAnalyticsApi__TimeoutSeconds` | MCP-to-API HTTP timeout in seconds. |

The Measurement Protocol API secret is not enough to read Analytics reports. To retrieve actual report data, configure `GoogleAnalytics__PropertyId` plus OAuth refresh-token credentials, service-account JSON, or an OAuth access token.
When multiple report credentials are present, OAuth refresh-token credentials are preferred, then service-account JSON, then a raw access token.

## Local Commands

```bash
dotnet restore Roggemans.GoogleAnalytics.Services.sln
dotnet test Roggemans.GoogleAnalytics.Services.sln
dotnet run --project Roggemans.GoogleAnalytics.API/Roggemans.GoogleAnalytics.API.csproj
dotnet run --project Roggemans.GoogleAnalytics.Mcp/Roggemans.GoogleAnalytics.Mcp.csproj
```

Run the API before the MCP locally, or set `GoogleAnalyticsApi__BaseUrl` to a reachable deployed API URL.

## Deployment

`.github/workflows/deploy-googleanalytics-api-to-ovh.yml` builds, tests, creates a Docker image, copies it to the VPS, starts a new container on a free host port starting at `8107`, and updates Caddy for `GoogleAnalyticsAPI.Test.Roggemans.com`.

`.github/workflows/deploy-googleanalytics-mcp-to-ovh.yml` deploys the MCP host on a free host port starting at `8108`, routes `GoogleAnalytics.MCP.test.Roggemans.com` through Caddy, connects it to the API over the `roggemans-googleanalytics` Docker network, and uses `GOOGLE_ANALYTICS_MCP_API_KEY` for the MCP API key.

Required GitHub secrets:

- `OVH_VPS_HOST`
- `OVH_VPS_USER`
- `OVH_VPS_SSH_KEY`
- `GOOGLE_ANALYTICS_MEASUREMENT_PROTOCOL_API_SECRET_DIVINTAGE`
- `GOOGLE_ANALYTICS_MCP_API_KEY`

Optional GitHub variable/secret pairs:

- `GOOGLE_ANALYTICS_MEASUREMENT_ID_DIVINTAGE`
- `GOOGLE_ANALYTICS_PROPERTY_ID_DIVINTAGE`
- `GOOGLE_ANALYTICS_SERVICE_ACCOUNT_JSON_BASE64_DIVINTAGE`
- `GOOGLE_ANALYTICS_OAUTH_CLIENT_ID_DIVINTAGE`
- `GOOGLE_ANALYTICS_OAUTH_CLIENT_SECRET_DIVINTAGE`
- `GOOGLE_ANALYTICS_OAUTH_REFRESH_TOKEN_DIVINTAGE`
- `GOOGLE_ANALYTICS_ACCESS_TOKEN_DIVINTAGE`
- `GOOGLE_ANALYTICS_REQUIRE_LIVE_REPORTS_DIVINTAGE`
- `GOOGLE_ANALYTICS_API_BASE_URL`
- `GOOGLE_ANALYTICS_DOCKER_NETWORK`
- `GOOGLE_ANALYTICS_API_CONTAINER_NAME`
- `GOOGLE_ANALYTICS_MCP_HOSTNAME`
- `GOOGLE_ANALYTICS_MCP_PORT`

Set `GOOGLE_ANALYTICS_REQUIRE_LIVE_REPORTS_DIVINTAGE=true` only when CI should fail if the configured reporting credential cannot read GA4 report data.
`GOOGLE_ANALYTICS_SERVICE_ACCOUNT_JSON_BASE64_DIVINTAGE` must be the base64 of the full downloaded Google service-account JSON file, not only the private key or key id.
