using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Roggemans.GoogleAnalytics.Services;

namespace Roggemans.GoogleAnalytics.Mcp.Features.Mcp;

public sealed class GoogleAnalyticsMcpRequestHandler
{
    private const string JsonRpcVersion = "2.0";
    private const string ProtocolVersion = "2024-11-05";
    private const int InvalidRequestCode = -32600;
    private const int MethodNotFoundCode = -32601;
    private const int InvalidParamsCode = -32602;
    private const int InternalErrorCode = -32603;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IGoogleAnalyticsReportService _googleAnalytics;

    public GoogleAnalyticsMcpRequestHandler(IGoogleAnalyticsReportService googleAnalytics)
    {
        _googleAnalytics = googleAnalytics;
    }

    public async Task<IDictionary<string, object?>> HandleAsync(
        JsonElement request,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (request.ValueKind != JsonValueKind.Object)
        {
            return ErrorResponse(null, InvalidRequestCode, "Request must be a JSON object.");
        }

        if (!request.TryGetProperty("method", out JsonElement methodElement)
            || methodElement.ValueKind != JsonValueKind.String)
        {
            return ErrorResponse(ReadId(request), InvalidRequestCode, "Missing JSON-RPC method.");
        }

        string? method = methodElement.GetString();
        JsonElement? id = ReadId(request);

        try
        {
            return method switch
            {
                "initialize" => ResultResponse(id, BuildInitializeResult(options)),
                "tools/list" => ResultResponse(id, new { tools = BuildToolDefinitions() }),
                "tools/call" => ResultResponse(
                    id,
                    await HandleToolCallAsync(
                        request,
                        sessionId,
                        options,
                        sessionStore,
                        cancellationToken)),
                "resources/list" => ResultResponse(id, new { resources = Array.Empty<object>() }),
                "resources/read" => ErrorResponse(id, MethodNotFoundCode, "Resources are not supported."),
                "prompts/list" => ResultResponse(id, new { prompts = Array.Empty<object>() }),
                "prompts/get" => ErrorResponse(id, MethodNotFoundCode, "Prompts are not supported."),
                _ => ErrorResponse(id, MethodNotFoundCode, $"Method \"{method}\" is not supported.")
            };
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid MCP request parameters.");
            return ErrorResponse(id, InvalidParamsCode, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled MCP error.");
            return ErrorResponse(id, InternalErrorCode, ex.Message);
        }
    }

    private static object BuildInitializeResult(GoogleAnalyticsMcpOptions options)
    {
        return new
        {
            protocolVersion = ProtocolVersion,
            capabilities = new
            {
                tools = new { listChanged = false }
            },
            serverInfo = new
            {
                name = options.ServerName,
                version = "0.1.0"
            }
        };
    }

    private static IReadOnlyList<object> BuildToolDefinitions()
    {
        return
        [
            Tool(
                "set_tracking_context",
                "Store the GA4 client/user/session context for this MCP session.",
                WithCommon(
                    ("clientId", StringSchema("GA4 client id, usually the browser client id.")),
                    ("userId", StringSchema("Optional internal anonymous user id. Do not send PII.")),
                    ("sessionId", StringSchema("Optional website session id used as GA4 session_id."))),
                ["clientId"]),
            Tool(
                "track_page_view",
                "Track a website page_view event.",
                WithCommon(
                    ("pageLocation", StringSchema("Full URL of the viewed page.")),
                    ("pageTitle", StringSchema("Page title.")),
                    ("pageReferrer", StringSchema("Referrer URL.")),
                    ("pagePath", StringSchema("Path or route.")),
                    ("flowName", StringSchema("Optional website flow name.")),
                    ("analysisPoint", StringSchema("Optional internal analysis point label."))),
                ["pageLocation"]),
            Tool(
                "track_user_identified",
                "Track user identification, login, or sign-up flow progress.",
                WithCommon(
                    ("method", StringSchema("Identification method, such as email, google, password, guest.")),
                    ("status", StringSchema("Identification status, such as started, succeeded, failed.")),
                    ("analysisPoint", StringSchema("Optional internal analysis point label."))),
                ["userId"]),
            Tool(
                "track_flow_step",
                "Track a named step in a website funnel or user journey.",
                WithCommon(
                    ("flowName", StringSchema("Flow name, such as checkout, request_quote, account_setup.")),
                    ("stepName", StringSchema("Step name.")),
                    ("stepIndex", IntegerSchema("Step index in the flow.")),
                    ("status", StringSchema("Step status, such as viewed, completed, abandoned, failed.")),
                    ("pageLocation", StringSchema("Current page URL.")),
                    ("analysisPoint", StringSchema("Optional internal analysis point label."))),
                ["flowName", "stepName"]),
            Tool(
                "track_custom_event",
                "Track a custom GA4 Measurement Protocol event with arbitrary parameters.",
                WithCommon(
                    ("eventName", StringSchema("GA4 event name."))),
                ["eventName"]),
            Tool(
                "validate_tracking_event",
                "Validate a GA4 Measurement Protocol event through the debug endpoint without collecting it.",
                WithCommon(
                    ("eventName", StringSchema("GA4 event name to validate."))),
                ["eventName"]),
            Tool(
                "track_search",
                "Track a site search event and result count.",
                WithCommon(
                    ("searchTerm", StringSchema("Search query.")),
                    ("resultsCount", IntegerSchema("Number of search results.")),
                    ("filters", ObjectSchema("Applied filters.")),
                    ("sort", StringSchema("Sort mode.")),
                    ("pageLocation", StringSchema("Current page URL."))),
                ["searchTerm"]),
            Tool(
                "track_product_view",
                "Track a product detail view.",
                WithCommon(
                    ("itemId", StringSchema("Product id or SKU.")),
                    ("itemName", StringSchema("Product name.")),
                    ("itemCategory", StringSchema("Product category.")),
                    ("price", NumberSchema("Product price.")),
                    ("currency", StringSchema("Currency code.")),
                    ("pageLocation", StringSchema("Current page URL."))),
                ["itemId"]),
            Tool(
                "track_cart_event",
                "Track add_to_cart, remove_from_cart, or view_cart.",
                WithCommon(
                    ("action", StringSchema("Cart action: add_to_cart, remove_from_cart, view_cart.")),
                    ("itemId", StringSchema("Product id or SKU.")),
                    ("itemName", StringSchema("Product name.")),
                    ("quantity", NumberSchema("Quantity.")),
                    ("value", NumberSchema("Monetary value.")),
                    ("currency", StringSchema("Currency code."))),
                ["action"]),
            Tool(
                "track_checkout_step",
                "Track checkout flow progress.",
                WithCommon(
                    ("stepName", StringSchema("Checkout step name.")),
                    ("stepIndex", IntegerSchema("Checkout step index.")),
                    ("value", NumberSchema("Cart or order value.")),
                    ("currency", StringSchema("Currency code.")),
                    ("status", StringSchema("Step status, such as started, completed, failed."))),
                ["stepName"]),
            Tool(
                "track_purchase",
                "Track a GA4 purchase event.",
                WithCommon(
                    ("transactionId", StringSchema("Order or transaction id.")),
                    ("value", NumberSchema("Purchase value.")),
                    ("currency", StringSchema("Currency code.")),
                    ("tax", NumberSchema("Tax amount.")),
                    ("shipping", NumberSchema("Shipping amount.")),
                    ("items", ArraySchema("GA4 item objects."))),
                ["transactionId", "value", "currency"]),
            Tool(
                "track_form_interaction",
                "Track form starts, submits, validation errors, and abandons.",
                WithCommon(
                    ("formName", StringSchema("Form name.")),
                    ("action", StringSchema("Form action, such as start, submit, validation_error, abandon.")),
                    ("fieldName", StringSchema("Optional field name.")),
                    ("validationStatus", StringSchema("Validation status.")),
                    ("pageLocation", StringSchema("Current page URL."))),
                ["formName", "action"]),
            Tool(
                "track_error",
                "Track website errors as GA4 exception events.",
                WithCommon(
                    ("errorName", StringSchema("Error name.")),
                    ("errorMessage", StringSchema("Error message.")),
                    ("errorCode", StringSchema("Application error code.")),
                    ("fatal", BooleanSchema("Whether the error was fatal.")),
                    ("pageLocation", StringSchema("Current page URL."))),
                ["errorName"])
        ];
    }

    private async Task<object> HandleToolCallAsync(
        JsonElement request,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        if (!request.TryGetProperty("params", out JsonElement paramsElement)
            || paramsElement.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Tool call parameters are required.", nameof(request));
        }

        if (!paramsElement.TryGetProperty("name", out JsonElement nameElement)
            || nameElement.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException("Tool name is required.", nameof(request));
        }

        string toolName = nameElement.GetString()!;
        JsonElement arguments = paramsElement.TryGetProperty("arguments", out JsonElement argumentsElement)
            && argumentsElement.ValueKind == JsonValueKind.Object
                ? argumentsElement
                : default;

        object result = toolName switch
        {
            "set_tracking_context" => HandleSetTrackingContext(arguments, sessionId, sessionStore),
            "track_page_view" => await TrackPageViewAsync(arguments, sessionId, options, sessionStore, cancellationToken),
            "track_user_identified" => await TrackUserIdentifiedAsync(arguments, sessionId, options, sessionStore, cancellationToken),
            "track_flow_step" => await TrackFlowStepAsync(arguments, sessionId, options, sessionStore, cancellationToken),
            "track_custom_event" => await TrackCustomEventAsync(arguments, sessionId, options, sessionStore, false, cancellationToken),
            "validate_tracking_event" => await TrackCustomEventAsync(arguments, sessionId, options, sessionStore, true, cancellationToken),
            "track_search" => await TrackSearchAsync(arguments, sessionId, options, sessionStore, cancellationToken),
            "track_product_view" => await TrackProductViewAsync(arguments, sessionId, options, sessionStore, cancellationToken),
            "track_cart_event" => await TrackCartEventAsync(arguments, sessionId, options, sessionStore, cancellationToken),
            "track_checkout_step" => await TrackCheckoutStepAsync(arguments, sessionId, options, sessionStore, cancellationToken),
            "track_purchase" => await TrackPurchaseAsync(arguments, sessionId, options, sessionStore, cancellationToken),
            "track_form_interaction" => await TrackFormInteractionAsync(arguments, sessionId, options, sessionStore, cancellationToken),
            "track_error" => await TrackErrorAsync(arguments, sessionId, options, sessionStore, cancellationToken),
            _ => throw new ArgumentException($"Tool \"{toolName}\" is not supported.")
        };

        return BuildToolResult(result);
    }

    private static object HandleSetTrackingContext(
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpSessionStore sessionStore)
    {
        string clientId = RequireString(arguments, "clientId");
        string? userId = ReadOptionalString(arguments, "userId");
        string? trackingSessionId = ReadOptionalString(arguments, "sessionId");

        sessionStore.SetContext(sessionId, clientId, userId, trackingSessionId);

        return new
        {
            stored = true,
            clientId,
            hasUserId = !string.IsNullOrWhiteSpace(userId),
            hasSessionId = !string.IsNullOrWhiteSpace(trackingSessionId)
        };
    }

    private Task<object> TrackPageViewAsync(
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> parameters = BuildCommonParameters(arguments, sessionId, options, sessionStore);
        Add(parameters, "page_location", RequireString(arguments, "pageLocation"));
        Add(parameters, "page_title", ReadOptionalString(arguments, "pageTitle"));
        Add(parameters, "page_referrer", ReadOptionalString(arguments, "pageReferrer"));
        Add(parameters, "page_path", ReadOptionalString(arguments, "pagePath"));
        Add(parameters, "flow_name", ReadOptionalString(arguments, "flowName"));
        Add(parameters, "analysis_point", ReadOptionalString(arguments, "analysisPoint"));
        MergeCustomParameters(parameters, arguments);

        return TrackSingleEventAsync("page_view", parameters, arguments, sessionId, options, sessionStore, null, cancellationToken);
    }

    private Task<object> TrackUserIdentifiedAsync(
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        string userId = RequireString(arguments, "userId");
        sessionStore.SetContext(sessionId, ReadOptionalString(arguments, "clientId"), userId, ReadOptionalString(arguments, "sessionId"));

        Dictionary<string, object?> parameters = BuildCommonParameters(arguments, sessionId, options, sessionStore);
        Add(parameters, "method", ReadOptionalString(arguments, "method"));
        Add(parameters, "status", ReadOptionalString(arguments, "status"));
        Add(parameters, "analysis_point", ReadOptionalString(arguments, "analysisPoint"));
        MergeCustomParameters(parameters, arguments);

        return TrackSingleEventAsync("user_identified", parameters, arguments, sessionId, options, sessionStore, userId, cancellationToken);
    }

    private Task<object> TrackFlowStepAsync(
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> parameters = BuildCommonParameters(arguments, sessionId, options, sessionStore);
        Add(parameters, "flow_name", RequireString(arguments, "flowName"));
        Add(parameters, "step_name", RequireString(arguments, "stepName"));
        Add(parameters, "step_index", ReadOptionalInt(arguments, "stepIndex"));
        Add(parameters, "status", ReadOptionalString(arguments, "status"));
        Add(parameters, "page_location", ReadOptionalString(arguments, "pageLocation"));
        Add(parameters, "analysis_point", ReadOptionalString(arguments, "analysisPoint"));
        MergeCustomParameters(parameters, arguments);

        return TrackSingleEventAsync("flow_step", parameters, arguments, sessionId, options, sessionStore, null, cancellationToken);
    }

    private Task<object> TrackCustomEventAsync(
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        bool forceDebug,
        CancellationToken cancellationToken)
    {
        string eventName = RequireString(arguments, "eventName");
        Dictionary<string, object?> parameters = BuildCommonParameters(arguments, sessionId, options, sessionStore);
        MergeCustomParameters(parameters, arguments);

        return TrackSingleEventAsync(eventName, parameters, arguments, sessionId, options, sessionStore, null, cancellationToken, forceDebug);
    }

    private Task<object> TrackSearchAsync(
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> parameters = BuildCommonParameters(arguments, sessionId, options, sessionStore);
        Add(parameters, "search_term", RequireString(arguments, "searchTerm"));
        Add(parameters, "results_count", ReadOptionalInt(arguments, "resultsCount"));
        Add(parameters, "filters", ReadOptionalJsonValue(arguments, "filters"));
        Add(parameters, "sort", ReadOptionalString(arguments, "sort"));
        Add(parameters, "page_location", ReadOptionalString(arguments, "pageLocation"));
        MergeCustomParameters(parameters, arguments);

        return TrackSingleEventAsync("search", parameters, arguments, sessionId, options, sessionStore, null, cancellationToken);
    }

    private Task<object> TrackProductViewAsync(
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> item = BuildItem(arguments);
        Dictionary<string, object?> parameters = BuildCommonParameters(arguments, sessionId, options, sessionStore);
        Add(parameters, "currency", ReadOptionalString(arguments, "currency"));
        Add(parameters, "value", ReadOptionalDecimal(arguments, "price"));
        parameters["items"] = new[] { item };
        Add(parameters, "page_location", ReadOptionalString(arguments, "pageLocation"));
        MergeCustomParameters(parameters, arguments);

        return TrackSingleEventAsync("view_item", parameters, arguments, sessionId, options, sessionStore, null, cancellationToken);
    }

    private Task<object> TrackCartEventAsync(
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        string action = RequireString(arguments, "action");
        Dictionary<string, object?> parameters = BuildCommonParameters(arguments, sessionId, options, sessionStore);
        Add(parameters, "currency", ReadOptionalString(arguments, "currency"));
        Add(parameters, "value", ReadOptionalDecimal(arguments, "value"));

        if (!string.Equals(action, "view_cart", StringComparison.OrdinalIgnoreCase))
        {
            parameters["items"] = new[] { BuildItem(arguments) };
        }

        MergeCustomParameters(parameters, arguments);
        return TrackSingleEventAsync(action, parameters, arguments, sessionId, options, sessionStore, null, cancellationToken);
    }

    private Task<object> TrackCheckoutStepAsync(
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> parameters = BuildCommonParameters(arguments, sessionId, options, sessionStore);
        Add(parameters, "step_name", RequireString(arguments, "stepName"));
        Add(parameters, "step_index", ReadOptionalInt(arguments, "stepIndex"));
        Add(parameters, "status", ReadOptionalString(arguments, "status"));
        Add(parameters, "currency", ReadOptionalString(arguments, "currency"));
        Add(parameters, "value", ReadOptionalDecimal(arguments, "value"));
        MergeCustomParameters(parameters, arguments);

        return TrackSingleEventAsync("begin_checkout", parameters, arguments, sessionId, options, sessionStore, null, cancellationToken);
    }

    private Task<object> TrackPurchaseAsync(
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> parameters = BuildCommonParameters(arguments, sessionId, options, sessionStore);
        Add(parameters, "transaction_id", RequireString(arguments, "transactionId"));
        Add(parameters, "value", RequireDecimal(arguments, "value"));
        Add(parameters, "currency", RequireString(arguments, "currency"));
        Add(parameters, "tax", ReadOptionalDecimal(arguments, "tax"));
        Add(parameters, "shipping", ReadOptionalDecimal(arguments, "shipping"));
        Add(parameters, "items", ReadOptionalJsonValue(arguments, "items"));
        MergeCustomParameters(parameters, arguments);

        return TrackSingleEventAsync("purchase", parameters, arguments, sessionId, options, sessionStore, null, cancellationToken);
    }

    private Task<object> TrackFormInteractionAsync(
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> parameters = BuildCommonParameters(arguments, sessionId, options, sessionStore);
        Add(parameters, "form_name", RequireString(arguments, "formName"));
        Add(parameters, "form_action", RequireString(arguments, "action"));
        Add(parameters, "field_name", ReadOptionalString(arguments, "fieldName"));
        Add(parameters, "validation_status", ReadOptionalString(arguments, "validationStatus"));
        Add(parameters, "page_location", ReadOptionalString(arguments, "pageLocation"));
        MergeCustomParameters(parameters, arguments);

        return TrackSingleEventAsync("form_interaction", parameters, arguments, sessionId, options, sessionStore, null, cancellationToken);
    }

    private Task<object> TrackErrorAsync(
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        CancellationToken cancellationToken)
    {
        Dictionary<string, object?> parameters = BuildCommonParameters(arguments, sessionId, options, sessionStore);
        Add(parameters, "description", RequireString(arguments, "errorName"));
        Add(parameters, "error_message", ReadOptionalString(arguments, "errorMessage"));
        Add(parameters, "error_code", ReadOptionalString(arguments, "errorCode"));
        Add(parameters, "fatal", ReadOptionalBool(arguments, "fatal") ?? false);
        Add(parameters, "page_location", ReadOptionalString(arguments, "pageLocation"));
        MergeCustomParameters(parameters, arguments);

        return TrackSingleEventAsync("exception", parameters, arguments, sessionId, options, sessionStore, null, cancellationToken);
    }

    private async Task<object> TrackSingleEventAsync(
        string eventName,
        IReadOnlyDictionary<string, object?> parameters,
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore,
        string? userIdOverride,
        CancellationToken cancellationToken,
        bool forceDebug = false)
    {
        TrackingContext context = sessionStore.GetOrCreate(sessionId);
        string? clientId = ReadOptionalString(arguments, "clientId") ?? context.ClientId ?? options.DefaultClientId;
        string? userId = userIdOverride ?? ReadOptionalString(arguments, "userId") ?? context.UserId;
        bool debugMode = forceDebug || ReadOptionalBool(arguments, "debugMode") == true;
        bool? nonPersonalizedAds = ReadOptionalBool(arguments, "nonPersonalizedAds");
        IReadOnlyDictionary<string, object?>? userProperties = ReadOptionalDictionary(arguments, "userProperties");

        GoogleAnalyticsTrackingRequest trackingRequest = new(
            clientId,
            userId,
            [new GoogleAnalyticsTrackingEvent(eventName, parameters)],
            userProperties,
            debugMode,
            nonPersonalizedAds);

        GoogleAnalyticsOperationResult<GoogleAnalyticsTrackingResult> result =
            await _googleAnalytics.TrackAsync(trackingRequest, cancellationToken).ConfigureAwait(false);

        return ToToolResponse(result);
    }

    private static Dictionary<string, object?> BuildCommonParameters(
        JsonElement arguments,
        string? sessionId,
        GoogleAnalyticsMcpOptions options,
        GoogleAnalyticsMcpSessionStore sessionStore)
    {
        TrackingContext context = sessionStore.GetOrCreate(sessionId);
        Dictionary<string, object?> parameters = new(StringComparer.Ordinal)
        {
            ["engagement_time_msec"] = ReadOptionalInt(arguments, "engagementTimeMsec") ?? 1
        };

        string? trackingSessionId = ReadOptionalString(arguments, "sessionId") ?? context.SessionId;
        Add(parameters, "session_id", trackingSessionId);
        Add(parameters, "client_context", ReadOptionalString(arguments, "clientContext"));
        Add(parameters, "mcp_server", options.ServerName);

        return parameters;
    }

    private static Dictionary<string, object?> BuildItem(JsonElement arguments)
    {
        Dictionary<string, object?> item = new(StringComparer.Ordinal);
        Add(item, "item_id", RequireString(arguments, "itemId"));
        Add(item, "item_name", ReadOptionalString(arguments, "itemName"));
        Add(item, "item_category", ReadOptionalString(arguments, "itemCategory"));
        Add(item, "price", ReadOptionalDecimal(arguments, "price"));
        Add(item, "quantity", ReadOptionalDecimal(arguments, "quantity"));
        return item;
    }

    private static object ToToolResponse<T>(GoogleAnalyticsOperationResult<T> result)
    {
        return result.Success
            ? new { success = true, data = result.Data }
            : new
            {
                success = false,
                errorCode = result.ErrorCode,
                errorMessage = result.ErrorMessage,
                statusCode = result.StatusCode is null ? null : (int?)result.StatusCode
            };
    }

    private static object BuildToolResult(object result)
    {
        return new
        {
            content = new[]
            {
                new
                {
                    type = "text",
                    text = JsonSerializer.Serialize(result, JsonOptions)
                }
            }
        };
    }

    private static void MergeCustomParameters(Dictionary<string, object?> parameters, JsonElement arguments)
    {
        IReadOnlyDictionary<string, object?>? customParameters = ReadOptionalDictionary(arguments, "parameters");
        if (customParameters is null)
        {
            return;
        }

        foreach (KeyValuePair<string, object?> parameter in customParameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.Key))
            {
                parameters[parameter.Key] = parameter.Value;
            }
        }
    }

    private static void Add(Dictionary<string, object?> parameters, string name, object? value)
    {
        if (value is null)
        {
            return;
        }

        if (value is string stringValue)
        {
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return;
            }

            parameters[name] = stringValue.Trim();
            return;
        }

        parameters[name] = value;
    }

    private static string RequireString(JsonElement arguments, string propertyName)
    {
        string? value = ReadOptionalString(arguments, propertyName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{propertyName} is required.");
        }

        return value;
    }

    private static decimal RequireDecimal(JsonElement arguments, string propertyName)
    {
        decimal? value = ReadOptionalDecimal(arguments, propertyName);
        if (!value.HasValue)
        {
            throw new ArgumentException($"{propertyName} is required.");
        }

        return value.Value;
    }

    private static string? ReadOptionalString(JsonElement arguments, string propertyName)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(propertyName, out JsonElement element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static int? ReadOptionalInt(JsonElement arguments, string propertyName)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(propertyName, out JsonElement element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int intValue))
        {
            return intValue;
        }

        if (element.ValueKind == JsonValueKind.String
            && int.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }

        return null;
    }

    private static decimal? ReadOptionalDecimal(JsonElement arguments, string propertyName)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(propertyName, out JsonElement element))
        {
            return null;
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out decimal decimalValue))
        {
            return decimalValue;
        }

        if (element.ValueKind == JsonValueKind.String
            && decimal.TryParse(element.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed))
        {
            return parsed;
        }

        return null;
    }

    private static bool? ReadOptionalBool(JsonElement arguments, string propertyName)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(propertyName, out JsonElement element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(element.GetString(), out bool parsed) => parsed,
            _ => null
        };
    }

    private static IReadOnlyDictionary<string, object?>? ReadOptionalDictionary(JsonElement arguments, string propertyName)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(propertyName, out JsonElement element)
            || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return element
            .EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => ReadJsonValue(property.Value),
                StringComparer.Ordinal);
    }

    private static object? ReadOptionalJsonValue(JsonElement arguments, string propertyName)
    {
        if (arguments.ValueKind != JsonValueKind.Object
            || !arguments.TryGetProperty(propertyName, out JsonElement element))
        {
            return null;
        }

        return ReadJsonValue(element);
    }

    private static object? ReadJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out long longValue) => longValue,
            JsonValueKind.Number when element.TryGetDecimal(out decimal decimalValue) => decimalValue,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Object => element
                .EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => ReadJsonValue(property.Value),
                    StringComparer.Ordinal),
            JsonValueKind.Array => element
                .EnumerateArray()
                .Select(ReadJsonValue)
                .ToArray(),
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static Dictionary<string, object> WithCommon(params (string Name, object Schema)[] properties)
    {
        Dictionary<string, object> schema = CommonProperties();
        foreach ((string name, object propertySchema) in properties)
        {
            schema[name] = propertySchema;
        }

        return schema;
    }

    private static Dictionary<string, object> CommonProperties()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["clientId"] = StringSchema("GA4 client id. Falls back to set_tracking_context or Mcp:DefaultClientId."),
            ["userId"] = StringSchema("Optional internal anonymous user id. Do not send PII."),
            ["sessionId"] = StringSchema("Optional GA4 session_id for the website session."),
            ["debugMode"] = BooleanSchema("When true, uses the Measurement Protocol debug endpoint."),
            ["engagementTimeMsec"] = IntegerSchema("GA4 engagement_time_msec. Defaults to 1."),
            ["clientContext"] = StringSchema("Optional source/context label, such as web, api, mcp, checkout-widget."),
            ["nonPersonalizedAds"] = BooleanSchema("Optional GA4 non_personalized_ads flag."),
            ["userProperties"] = ObjectSchema("Optional GA4 user_properties object."),
            ["parameters"] = ObjectSchema("Optional extra GA4 event parameters.")
        };
    }

    private static object Tool(
        string name,
        string description,
        Dictionary<string, object> properties,
        string[]? required = null)
    {
        Dictionary<string, object?> inputSchema = new(StringComparer.Ordinal)
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false
        };

        if (required is { Length: > 0 })
        {
            inputSchema["required"] = required;
        }

        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = name,
            ["description"] = description,
            ["inputSchema"] = inputSchema
        };
    }

    private static object StringSchema(string description) => new { type = "string", description };

    private static object IntegerSchema(string description) => new { type = "integer", description };

    private static object NumberSchema(string description) => new { type = "number", description };

    private static object BooleanSchema(string description) => new { type = "boolean", description };

    private static object ObjectSchema(string description) => new
    {
        type = "object",
        description,
        additionalProperties = true
    };

    private static object ArraySchema(string description) => new
    {
        type = "array",
        description,
        items = new
        {
            type = "object",
            additionalProperties = true
        }
    };

    private static JsonElement? ReadId(JsonElement request)
    {
        return request.TryGetProperty("id", out JsonElement idElement) ? idElement.Clone() : null;
    }

    private static Dictionary<string, object?> ResultResponse(JsonElement? id, object result)
    {
        return new Dictionary<string, object?>
        {
            ["jsonrpc"] = JsonRpcVersion,
            ["id"] = id.HasValue ? id.Value : null,
            ["result"] = result
        };
    }

    private static Dictionary<string, object?> ErrorResponse(JsonElement? id, int code, string message)
    {
        return new Dictionary<string, object?>
        {
            ["jsonrpc"] = JsonRpcVersion,
            ["id"] = id.HasValue ? id.Value : null,
            ["error"] = new { code, message }
        };
    }
}
