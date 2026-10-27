using System.Text.Json;

namespace RequestTracker.Api.ErrorHandling;

internal static class ErrorKeys
{
    // "Items[0].Version" → "items[0].version": every segment, not only the first character. A failure that
    // isn't about one property goes under "", like model-level errors from model binding.
    // JSON body binding errors arrive as "$.version"; the "$." root is dropped so they use the same key as the
    // validator's errors for that property.
    public static string ToJsonPath(string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return string.Empty;
        }

        var path = propertyName.StartsWith("$.", StringComparison.Ordinal) ? propertyName[2..] : propertyName;
        return string.Join('.', path.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
    }
}
