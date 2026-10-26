using System.Text.Json;

namespace RequestTracker.Api.ErrorHandling;

internal static class ErrorKeys
{
    // "Items[0].Version" → "items[0].version": every segment, not only the first character. A failure that
    // isn't about one property goes under "", like model-level errors from model binding.
    public static string ToJsonPath(string? propertyName) =>
        string.IsNullOrEmpty(propertyName)
            ? string.Empty
            : string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
