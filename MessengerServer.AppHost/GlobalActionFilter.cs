using MessengerServer.AppHost.APIResponse;
using MessengerServer.AppHost.Logging;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MessengerServer.AppHost
{
    public class GlobalActionFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            foreach (KeyValuePair<string, object?> arg in context.ActionArguments)
            {
                string lowerStr = context.HttpContext.Request.Method.ToLower();
                string method = lowerStr[0].ToString().ToUpper() + lowerStr[1..];

                HttpRequestType httpRequestType = Enum.Parse<HttpRequestType>(method);
                JsonNode jsonNode = JsonNode.Parse(JsonSerializer.Serialize(arg.Value, Server.JsonSerializerOptions))!;

                Type? unboxedType = arg.Value?.GetType();
                if (unboxedType is not null)
                {
                    Logger.LogHttpPayload(unboxedType, PayloadType.Received, httpRequestType, jsonNode);
                }
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            string lowerStr = context.HttpContext.Request.Method.ToLower();
            string method = lowerStr[0].ToString().ToUpper() + lowerStr[1..];
            HttpRequestType httpRequestType = Enum.Parse<HttpRequestType>(method);

            if (context.Result is ObjectResult objectResult)
            {
                object? value = objectResult.Value;
                JsonNode jsonNode = JsonNode.Parse(JsonSerializer.Serialize(value, Server.JsonSerializerOptions))!;

                Type? apiResponseType = value?.GetType();
                if (apiResponseType is null)
                {
                    return;
                }

                Type? unboxedType = apiResponseType.GetProperty(APIResponse<object>.GetDataPropertyName())?.GetType();
                if (unboxedType is not null)
                {
                    Logger.LogHttpPayload(unboxedType, PayloadType.Sent, httpRequestType, jsonNode);
                }
            }
            else if (context.Result is JsonResult jsonResult)
            {
                object? value = jsonResult.Value;
                string json = JsonSerializer.Serialize(value);

                Type? apiResponseType = value?.GetType();
                if (apiResponseType is null)
                {
                    return;
                }

                Type? unboxedType = apiResponseType.GetProperty(APIResponse<object>.GetDataPropertyName())?.GetType();
                if (unboxedType is not null)
                {
                    Logger.LogHttpPayload(unboxedType, PayloadType.Sent, httpRequestType, json);
                }
            }
        }
    }
}
