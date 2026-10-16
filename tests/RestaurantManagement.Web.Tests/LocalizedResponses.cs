using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Sends a request in a chosen language (Accept-Language) and reads the messages back from the JSON
/// body. Bodies are parsed, never compared as raw text, because JSON escapes accented letters.
/// </summary>
public static class LocalizedResponses
{
    public static HttpRequestMessage Request(string method, string url, string culture, string? jsonBody = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture));
        if (jsonBody is not null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return request;
    }

    public static HttpRequestMessage Request(string method, string url, string culture, object body)
    {
        var request = Request(method, url, culture);
        request.Content = JsonContent.Create(body);
        return request;
    }

    /// <summary>The "message" property of an error body such as 401, 404 or 500.</summary>
    public static async Task<string?> MessageAsync(HttpResponseMessage response)
        => JsonNode.Parse(await response.Content.ReadAsStringAsync())?["message"]?.GetValue<string>();

    /// <summary>Every field error of a validation (400) body, from all fields.</summary>
    public static async Task<List<string>> FieldErrorsAsync(HttpResponseMessage response)
    {
        var errors = JsonNode.Parse(await response.Content.ReadAsStringAsync())?["errors"]?.AsObject();
        return errors?.SelectMany(field => field.Value!.AsArray().Select(message => message!.GetValue<string>())).ToList()
            ?? new List<string>();
    }

    /// <summary>The field errors of a validation (400) body reported under one field.</summary>
    public static async Task<List<string>> FieldErrorsAsync(HttpResponseMessage response, string field)
    {
        var messages = JsonNode.Parse(await response.Content.ReadAsStringAsync())?["errors"]?[field]?.AsArray();
        return messages?.Select(message => message!.GetValue<string>()).ToList() ?? new List<string>();
    }
}
