# Roggemans.GoogleAnalytics.Services

Proof-of-concept Google Analytics service and API for DiVintage.

The API exposes:

- `GET /health`
- `GET /config`
- `GET /api/google-analytics/divintage/summary`
- `POST /api/google-analytics/measurement-protocol/validate`

## Runtime Configuration

The repository does not store Google Analytics secrets. Configure them with environment variables or GitHub Actions secrets:

| Setting | Purpose |
| --- | --- |
| `GoogleAnalytics__MeasurementId` | GA4 measurement id, defaulted to DiVintage `G-XJWB055WRG`. |
| `GoogleAnalytics__MeasurementProtocolApiSecret` | GA4 Measurement Protocol API secret. |
| `GoogleAnalytics__PropertyId` | GA4 property id for Data API reports. |
| `GoogleAnalytics__ServiceAccountJsonBase64` | Base64-encoded Google service-account JSON with Analytics read access. |
| `GoogleAnalytics__AccessToken` | Optional OAuth access token alternative for short-lived tests. |

The Measurement Protocol API secret is not enough to read Analytics reports. To retrieve actual report data, configure `GoogleAnalytics__PropertyId` plus service-account JSON or an OAuth access token.
When both are present, service-account JSON is preferred because raw OAuth access tokens are short-lived.

## Local Commands

```bash
dotnet restore Roggemans.GoogleAnalytics.Services.sln
dotnet test Roggemans.GoogleAnalytics.Services.sln
dotnet run --project Roggemans.GoogleAnalytics.API/Roggemans.GoogleAnalytics.API.csproj
```

## Deployment

`.github/workflows/deploy-googleanalytics-api-to-ovh.yml` builds, tests, creates a Docker image, copies it to the VPS, starts a new container on a free host port starting at `8107`, and updates Caddy for `GoogleAnalyticsAPI.Test.Roggemans.com`.

Required GitHub secrets:

- `OVH_VPS_HOST`
- `OVH_VPS_USER`
- `OVH_VPS_SSH_KEY`
- `GOOGLE_ANALYTICS_MEASUREMENT_PROTOCOL_API_SECRET_DIVINTAGE`

Optional GitHub variable/secret pairs:

- `GOOGLE_ANALYTICS_MEASUREMENT_ID_DIVINTAGE`
- `GOOGLE_ANALYTICS_PROPERTY_ID_DIVINTAGE`
- `GOOGLE_ANALYTICS_SERVICE_ACCOUNT_JSON_BASE64_DIVINTAGE`
- `GOOGLE_ANALYTICS_ACCESS_TOKEN_DIVINTAGE`
- `GOOGLE_ANALYTICS_REQUIRE_LIVE_REPORTS_DIVINTAGE`

Set `GOOGLE_ANALYTICS_REQUIRE_LIVE_REPORTS_DIVINTAGE=true` only when CI should fail if the configured reporting credential cannot read GA4 report data.
`GOOGLE_ANALYTICS_SERVICE_ACCOUNT_JSON_BASE64_DIVINTAGE` must be the base64 of the full downloaded Google service-account JSON file, not only the private key or key id.
