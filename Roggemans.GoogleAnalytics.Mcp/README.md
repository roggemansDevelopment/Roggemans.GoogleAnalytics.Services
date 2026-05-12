# Roggemans.GoogleAnalytics.Mcp

HTTP/SSE MCP server for DiVintage Google Analytics tracking and reporting.

The server follows the same JSON-RPC over `/mcp` pattern used by the SpecsDrivenDevelopment MCP projects. Tool handlers consume `Roggemans.GoogleAnalytics.API` through `Roggemans.GoogleAnalytics.ApiClient`.

## Configuration

- `Mcp__ApiKey`: optional MCP API key, sent with `X-Mcp-ApiKey`.
- `Mcp__DefaultClientId`: fallback GA4 client id when a tool call does not provide one.
- `GoogleAnalyticsApi__BaseUrl`: base URL for the Google Analytics API, for example `http://localhost:5188/` locally or a container DNS URL in Docker.
- `GoogleAnalyticsApi__TimeoutSeconds`: MCP-to-API HTTP timeout in seconds.

## Tools

- `get_configuration_status`
- `get_divintage_summary`
- `validate_measurement_protocol`
- `set_tracking_context`
- `track_page_view`
- `track_user_identified`
- `track_flow_step`
- `track_custom_event`
- `validate_tracking_event`
- `track_search`
- `track_product_view`
- `track_cart_event`
- `track_checkout_step`
- `track_purchase`
- `track_form_interaction`
- `track_error`

## Run

```bash
dotnet run --project Roggemans.GoogleAnalytics.Mcp/Roggemans.GoogleAnalytics.Mcp.csproj
```

## Deploy

The GitHub Actions workflow `deploy-googleanalytics-mcp-to-ovh.yml` deploys this host to `GoogleAnalytics.MCP.test.Roggemans.com` by default. It expects `GOOGLE_ANALYTICS_MCP_API_KEY` as the API key secret, binds the container to a free host port starting at `8108`, and calls the API over the shared `roggemans-googleanalytics` Docker network.
