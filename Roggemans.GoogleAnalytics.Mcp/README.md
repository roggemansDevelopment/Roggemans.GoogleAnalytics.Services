# Roggemans.GoogleAnalytics.Mcp

HTTP/SSE MCP server for DiVintage Google Analytics tracking.

The server follows the same JSON-RPC over `/mcp` pattern used by the SpecsDrivenDevelopment MCP projects.

## Configuration

- `Mcp__ApiKey`: optional MCP API key, sent with `X-Mcp-ApiKey`.
- `Mcp__DefaultClientId`: fallback GA4 client id when a tool call does not provide one.
- `GoogleAnalytics__MeasurementId`: GA4 measurement id.
- `GoogleAnalytics__MeasurementProtocolApiSecret`: GA4 Measurement Protocol API secret.

## Tools

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
