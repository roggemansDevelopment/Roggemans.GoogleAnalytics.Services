# Roggemans.GoogleAnalytics.Services

This repository contains a .NET 10 proof-of-concept Google Analytics service/API for DiVintage.

## Project Layout

- `Roggemans.GoogleAnalytics.Services`: reusable service library.
- `Roggemans.GoogleAnalytics.API`: minimal API host and Docker entrypoint.
- `Roggemans.GoogleAnalytics.Services.Tests`: unit tests and opt-in live probes.

## Secret Handling

Never commit Google Analytics credentials. Use environment variables, .NET user secrets, or GitHub Actions secrets:

- `GoogleAnalytics__MeasurementId`
- `GoogleAnalytics__MeasurementProtocolApiSecret`
- `GoogleAnalytics__PropertyId`
- `GoogleAnalytics__ServiceAccountJsonBase64`
- `GoogleAnalytics__AccessToken`

The Measurement Protocol API secret can validate Measurement Protocol payloads, but Google Analytics report retrieval requires a GA4 property id and OAuth/service-account read credentials.

## Validation

Run:

```bash
dotnet test Roggemans.GoogleAnalytics.Services.sln
```

Live tests skip their external call when the required runtime configuration is not present.
