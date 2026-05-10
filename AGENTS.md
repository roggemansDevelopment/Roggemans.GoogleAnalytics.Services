# Roggemans.GoogleAnalytics.Services

This repository contains a .NET 10 proof-of-concept Google Analytics service/API for DiVintage.

## Project Layout

- `Roggemans.GoogleAnalytics.Services`: reusable service library.
- `Roggemans.GoogleAnalytics.API`: minimal API host and Docker entrypoint.
- `Roggemans.GoogleAnalytics.Mcp`: HTTP/SSE MCP host for GA4 Measurement Protocol tracking tools.
- `Roggemans.GoogleAnalytics.Services.Tests`: unit tests and opt-in live probes.

## Secret Handling

Never commit Google Analytics credentials. Use internal ASP.NET configuration keys in the app and DiVintage-specific names for GitHub Actions secrets/variables.

Internal app configuration keys:

- `GoogleAnalytics__MeasurementId`
- `GoogleAnalytics__MeasurementProtocolApiSecret`
- `GoogleAnalytics__PropertyId`
- `GoogleAnalytics__ServiceAccountJsonBase64`
- `GoogleAnalytics__OAuthClientId`
- `GoogleAnalytics__OAuthClientSecret`
- `GoogleAnalytics__OAuthRefreshToken`
- `GoogleAnalytics__AccessToken`

GitHub Actions names:

- `GOOGLE_ANALYTICS_MEASUREMENT_ID_DIVINTAGE`
- `GOOGLE_ANALYTICS_MEASUREMENT_PROTOCOL_API_SECRET_DIVINTAGE`
- `GOOGLE_ANALYTICS_PROPERTY_ID_DIVINTAGE`
- `GOOGLE_ANALYTICS_SERVICE_ACCOUNT_JSON_BASE64_DIVINTAGE`
- `GOOGLE_ANALYTICS_OAUTH_CLIENT_ID_DIVINTAGE`
- `GOOGLE_ANALYTICS_OAUTH_CLIENT_SECRET_DIVINTAGE`
- `GOOGLE_ANALYTICS_OAUTH_REFRESH_TOKEN_DIVINTAGE`
- `GOOGLE_ANALYTICS_ACCESS_TOKEN_DIVINTAGE`
- `GOOGLE_ANALYTICS_REQUIRE_LIVE_REPORTS_DIVINTAGE`

The Measurement Protocol API secret can validate Measurement Protocol payloads, but Google Analytics report retrieval requires a GA4 property id and OAuth/service-account read credentials.
Set `GOOGLE_ANALYTICS_REQUIRE_LIVE_REPORTS_DIVINTAGE=true` only when CI should fail on an unsuccessful live reporting probe.
OAuth refresh-token credentials are preferred over service-account JSON, and service-account JSON is preferred over `GOOGLE_ANALYTICS_ACCESS_TOKEN_DIVINTAGE`. The base64 secret must contain the full downloaded Google service-account JSON file.
The MCP host follows the manual JSON-RPC over SSE pattern from the SpecsDrivenDevelopment MCP projects and exposes tool handlers for GA4 Measurement Protocol tracking.

## Validation

Run:

```bash
dotnet test Roggemans.GoogleAnalytics.Services.sln
```

Live tests skip their external call when the required runtime configuration is not present.
